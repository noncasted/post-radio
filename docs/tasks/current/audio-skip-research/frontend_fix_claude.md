# Frontend audio — план исправлений

Дата: 2026-04-23
Автор: Claude + Codex (объединённые выводы)
Источники: `claude_research.md`, `frontend_problem_codex.md`

## Контекст одной строкой

На проде иногда песня не доигрывает и скипается. Причина — плеер `AudioPlayer.razor` использует эвристики (`diff < 1`, 10-сек watchdog) вместо реальных HTML media events, плюс `SetNext()` не сериализован.

---

## Принципы плана

- Правим только frontend-плеер и RadioApi (узкий контур).
- Backend трогаем минимально: только `.tmp` + rename в `MediaStorage.SaveAudio` (hardening, не top-cause).
- Каждый приоритет — отдельный коммит, чтобы можно было откатить точечно.
- Никаких новых абстракций: патчим существующий `AudioPlayer.razor` и `RadioApi.cs`.
- Проверка — ручная в браузере + прогнать `dotnet build backend/post-radio.slnx`.

---

## P0 — Убрать ложные auto-skip триггеры

Цель: плеер перестаёт сам решать, что трек кончился/завис, по эвристикам. Переходит на реальные события `<audio>`.

### P0.1 Подписаться на полный набор media events

Файл: `backend/Frontend/Client/Components/AudioPlayer.razor`

JS-слой (`audioHelper.setAudioElement`):

```javascript
setAudioElement: (dotnetHelper) => {
    const audio = document.getElementById("audio");
    audio.ontimeupdate = () => {
        if (audio.currentTime == null || isNaN(audio.currentTime)) return;
        if (audio.duration == null || isNaN(audio.duration)) return;
        dotnetHelper.invokeMethodAsync("OnAudioProgress", audio.currentTime, audio.duration);
    };
    audio.onended = () => dotnetHelper.invokeMethodAsync("OnAudioEnded");
    audio.onerror = () => dotnetHelper.invokeMethodAsync(
        "OnAudioError",
        audio.error ? audio.error.code : 0);
    audio.onwaiting = () => dotnetHelper.invokeMethodAsync("OnAudioBuffering", true);
    audio.onstalled = () => dotnetHelper.invokeMethodAsync("OnAudioBuffering", true);
    audio.onplaying = () => dotnetHelper.invokeMethodAsync("OnAudioBuffering", false);
},
```

C#-хендлеры:

```csharp
private bool _isBuffering;

[JSInvokable]
public void OnAudioEnded() => _ = SetNext("audio-ended");

[JSInvokable]
public void OnAudioError(int code)
{
    Console.WriteLine($"[AudioPlayer] audio error code={code}");
    _ = SetNext("audio-error");
}

[JSInvokable]
public void OnAudioBuffering(bool buffering)
{
    _isBuffering = buffering;
    if (!buffering)
        _lastTimeUpdate = DateTime.UtcNow;
}
```

### P0.2 Убрать early-skip по `diff < 1`

Строки 142-143 удалить. `OnAudioProgress` теперь только обновляет watchdog:

```csharp
[JSInvokable]
public void OnAudioProgress(double currentTime, double duration)
{
    _lastTimeUpdate = DateTime.UtcNow;
}
```

Переход на следующий трек = только `onended`, `onerror`, явный `RequestSkip`, `PlaylistChanged`.

### P0.3 Watchdog с inspect, не слепой skip

Сейчас:

```csharp
if (DateTime.UtcNow - _lastTimeUpdate >= updateTimeSpan) return false;
```

Заменить на:

```csharp
bool Check()
{
    if (_audioUrl != url) return false;
    if (State.Token.IsCancellationRequested) return false;
    if (_isBuffering) return true;                       // пока буферизация — ждём
    if (DateTime.UtcNow - _lastTimeUpdate >= updateTimeSpan) return false;
    return true;
}
```

Плюс после выхода из `while (Check())` — не сразу `SetNext`, а inspect состояния:

```csharp
var reason = await Js.InvokeAsync<string>("inspectAudioState", url);
if (reason != null && _audioUrl == url)
    await SetNext(reason);
```

JS:

```javascript
async function inspectAudioState(expectedUrl) {
    const audio = document.getElementById("audio");
    if (!audio.getAttribute("src")) return "no-src";
    if (audio.error) return `media-error-${audio.error.code}`;
    if (audio.ended) return "ended-confirmed";
    if (audio.paused) return "paused-unexpected";
    if (audio.readyState < 2) return "not-ready";
    // реально ещё играет — не скипаем
    return null;
}
```

Если `inspectAudioState` вернул null — watchdog ложная тревога, не скипаем.

---

## P1 — Убрать race conditions в SetNext

Цель: при одновременных триггерах из `OnAudioEnded`, `OnAudioError`, `SkipRequested`, `PlaylistChanged`, watchdog — инкремент индекса происходит ровно один раз.

### P1.1 Сериализовать SetNext через SemaphoreSlim

Файл: `backend/Frontend/Client/Components/AudioPlayer.razor`

```csharp
private readonly SemaphoreSlim _setNextLock = new(1, 1);
private int _setNextGeneration;

private async Task SetNext(string reason)
{
    if (!await _setNextLock.WaitAsync(0))
    {
        Console.WriteLine($"[AudioPlayer] SetNext skipped (in-flight), reason={reason}");
        return;
    }

    try
    {
        var generation = Interlocked.Increment(ref _setNextGeneration);
        var song = State.IncSongIndex();
        State.SetCurrentSong(song);

        Console.WriteLine($"[AudioPlayer] SetNext reason={reason} songId={song?.Id}");

        if (song == null)
        {
            _audioUrl = string.Empty;
            await InvokeAsync(StateHasChanged);
            return;
        }

        var url = await Api.GetSongStreamUrl(song.Id);

        if (generation != _setNextGeneration) return;   // устарело — выброшен более новой SetNext

        _audioUrl = url;
        await InvokeAsync(StateHasChanged);
    }
    finally
    {
        _setNextLock.Release();
    }
}
```

`WaitAsync(0)` — non-blocking, дубликаты не встают в очередь, а отбрасываются с логом.

`generation`-токен — защищает от случая, когда медленный `GetSongStreamUrl` возвращается после того, как другой SetNext уже успел перезаписать `_audioUrl`.

### P1.2 Обновить все вызовы SetNext

Передать reason на каждом источнике:

```csharp
State.SkipRequested += () => _ = SetNext("manual-skip");
State.PlaylistChanged += () => _ = SetNext("playlist-changed");
```

В `Loop()`:

```csharp
if (string.IsNullOrWhiteSpace(_audioUrl))
    await SetNext("startup-empty-url");
// ...
if (!started)
{
    if (_audioUrl == url)
        await SetNext("play-failed");
    // ...
}
// ...
if (_audioUrl == url)
    await SetNext("loop-exit");
```

В `catch`:

```csharp
catch (Exception e)
{
    Console.WriteLine($"[AudioPlayer] exception in Loop: {e}");
    try
    {
        await Task.Delay(TimeSpan.FromSeconds(5), State.Token);
        await SetNext("exception-fallback");
    }
    catch (OperationCanceledException) { return; }
}
```

### P1.3 Generation-токен для OnAudioProgress

Чтобы `ontimeupdate` от старого трека не обновлял `_lastTimeUpdate` нового:

```csharp
private int _currentAudioGeneration;
```

JS передаёт snapshot `src` в callback:

```javascript
audio.ontimeupdate = () => {
    if (audio.currentTime == null || isNaN(audio.currentTime)) return;
    if (audio.duration == null || isNaN(audio.duration)) return;
    dotnetHelper.invokeMethodAsync(
        "OnAudioProgress",
        audio.currentTime,
        audio.duration,
        audio.currentSrc);
};
```

C#:

```csharp
[JSInvokable]
public void OnAudioProgress(double currentTime, double duration, string src)
{
    if (src != _audioUrl) return;   // stale event от предыдущего трека
    _lastTimeUpdate = DateTime.UtcNow;
}
```

То же применить к `OnAudioEnded`, `OnAudioError`, `OnAudioBuffering`.

---

## P2 — Устойчивость к transient backend failure

### P2.1 Retry той же песни при пустом URL

Файл: `backend/Frontend/Client/Components/AudioPlayer.razor`

Сейчас пустой `_audioUrl` → `SetNext()` → инкремент индекса → песня потеряна.

Ввести попытку retry того же song.Id до N раз, прежде чем инкрементить:

```csharp
private async Task<bool> TryLoadCurrentSongUrl(SongDto song)
{
    for (var attempt = 0; attempt < 3; attempt++)
    {
        var url = await Api.GetSongStreamUrl(song.Id);
        if (!string.IsNullOrWhiteSpace(url))
        {
            _audioUrl = url;
            return true;
        }
        await Task.Delay(TimeSpan.FromSeconds(1 << attempt), State.Token);
    }
    return false;
}
```

`SetNext` инкрементит индекс только если `TryLoadCurrentSongUrl` окончательно упал:

```csharp
private async Task SetNext(string reason)
{
    // ... блокировка, generation ...
    var song = State.IncSongIndex();
    State.SetCurrentSong(song);

    if (song == null)
    {
        _audioUrl = string.Empty;
        await InvokeAsync(StateHasChanged);
        return;
    }

    if (!await TryLoadCurrentSongUrl(song))
    {
        Console.WriteLine($"[AudioPlayer] SetNext: song {song.Id} unavailable, skipping to next");
        // здесь можно отметить песню как невалидную и продолжить цикл
        _audioUrl = string.Empty;
    }

    await InvokeAsync(StateHasChanged);
}
```

### P2.2 Правильный Dispose

Файл: `backend/Frontend/Client/Components/AudioPlayer.razor`

Проблема: при навигации/re-render родителя старый `Loop()` продолжает крутиться параллельно с новым компонентом → два `Loop` на один `<audio>`.

Ввести свой `CancellationTokenSource` и отписываться от событий:

```csharp
private CancellationTokenSource? _lifetimeCts;
private Action? _onStarted;
private Action? _onSkip;
private Action? _onPlaylist;
private Action? _onVolume;

protected override void OnInitialized()
{
    _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(State.Token);

    _onStarted = () => _ = Loop();
    _onSkip = () => _ = SetNext("manual-skip");
    _onPlaylist = () => _ = SetNext("playlist-changed");
    _onVolume = () => _ = OnVolumeChanged(State.Volume);

    State.Started += _onStarted;
    State.SkipRequested += _onSkip;
    State.PlaylistChanged += _onPlaylist;
    State.VolumeChanged += _onVolume;
}

public void Dispose()
{
    if (_onStarted != null) State.Started -= _onStarted;
    if (_onSkip != null) State.SkipRequested -= _onSkip;
    if (_onPlaylist != null) State.PlaylistChanged -= _onPlaylist;
    if (_onVolume != null) State.VolumeChanged -= _onVolume;

    _lifetimeCts?.Cancel();
    _lifetimeCts?.Dispose();
    _dotNetRef?.Dispose();
    _setNextLock.Dispose();
}
```

В `Loop()` использовать `_lifetimeCts.Token` везде где сейчас `State.Token`. Тогда Dispose однозначно останавливает именно этот экземпляр Loop.

### P2.3 Императивный setSource + ожидание canplay (опционально, если P0.3 не хватит)

Если после P0.3 всё равно будут странные гонки на старте, переехать с Blazor-binding на императивный setSource:

JS:

```javascript
async function setSource(url) {
    const audio = document.getElementById("audio");
    audio.src = url;
    await new Promise((resolve, reject) => {
        const onCanPlay = () => { cleanup(); resolve(); };
        const onError = () => { cleanup(); reject(new Error("load failed")); };
        const cleanup = () => {
            audio.removeEventListener("canplay", onCanPlay);
            audio.removeEventListener("error", onError);
        };
        audio.addEventListener("canplay", onCanPlay, { once: true });
        audio.addEventListener("error", onError, { once: true });
        audio.load();
    });
}
```

C#: заменить `<audio src="@_audioUrl">` на `<audio id="audio">` и в Loop:

```csharp
try { await Js.InvokeVoidAsync("setSource", _audioUrl); }
catch { await SetNext("load-failed"); continue; }

var started = await Js.InvokeAsync<bool>("play");
```

Причина, почему это помогает: устраняет `StateHasChanged + Task.Delay(100)` и даёт явный ready-gate перед `play()`.

Это опциональный шаг — сначала выкатить P0-P1 и посмотреть по логам, нужен ли он.

---

## P3 — Hardening и диагностика

### P3.1 Browser-лог событий `<audio>` (на время отладки)

JS (только под флагом):

```javascript
function enableAudioDiagnostics() {
    const audio = document.getElementById("audio");
    const events = [
        "loadstart", "durationchange", "loadedmetadata", "canplay", "canplaythrough",
        "play", "playing", "pause", "waiting", "stalled", "suspend", "abort",
        "error", "ended", "seeking", "seeked", "emptied"
    ];
    for (const ev of events) {
        audio.addEventListener(ev, () => {
            console.log("[audio]", ev, {
                src: audio.currentSrc || audio.src,
                currentTime: audio.currentTime,
                duration: audio.duration,
                paused: audio.paused,
                ended: audio.ended,
                readyState: audio.readyState,
                networkState: audio.networkState,
                error: audio.error && { code: audio.error.code, message: audio.error.message }
            });
        });
    }
}
```

Вызывать один раз в `setAudioElement`. Снять browser-логи в проде/стейдже на час.

### P3.2 Decision tree для интерпретации логов

После выката P0-P1 посмотреть в логах `SetNext reason=...`:

- `audio-ended` — нормальный переход, хорошо;
- `audio-error` — смотреть `error.code` и backend-логи 404/500;
- `play-failed` — смотреть `NotSupportedError`/`AbortError` в browser console;
- `loop-exit` после inspect `null` — не должно быть, если есть — баг в inspect;
- два подряд SetNext с разными reason — значит serialize не сработал, расследовать.

### P3.3 MediaStorage atomic write (backend hardening)

Файл: `backend/Common/Storage/MediaStorage.cs`

```csharp
public async Task SaveAudio(long id, Stream stream)
{
    await EnsureStorage();

    var path = GetAudioPath(id);
    var tmpPath = path + ".tmp";

    await using (var file = new FileStream(tmpPath, FileMode.Create, FileAccess.Write,
                                            FileShare.None, 1024 * 128, useAsync: true))
    {
        await stream.CopyToAsync(file);
        await file.FlushAsync();
    }

    File.Move(tmpPath, path, overwrite: true);
}
```

Это не top-cause (см. диалог про SetLoaded), это защита от crash-recovery при аварийном завершении процесса во время записи.

Дополнительно: если tmp остался от прошлой жизни — убирать на старте (`Directory.EnumerateFiles(dir, "*.tmp")` → Delete).

### P3.4 Проверка prod media

Разово на prod volume:

```bash
find /var/lib/.../media/audio -name "*.mp3" -exec \
    ffmpeg -v warning -i {} -f null - \;
```

Если есть decode warnings — найти песни и принудительно перезалить.

---

## Порядок выката

| Коммит | Что | Зависимость |
|---|---|---|
| 1 | P0.1 + P0.2 + P0.3 (events, убрать `diff<1`, inspect) | — |
| 2 | P1.1 + P1.2 + P1.3 (serialize + generation + reason) | после 1 |
| 3 | P2.1 + P2.2 (retry, Dispose) | после 2 |
| 4 | P3.1 + P3.3 (diagnostics + atomic write) | после 3 |
| 5 | P2.3 императивный setSource | только если 1-4 недостаточно |

Каждый коммит — отдельный PR или git-коммит, чтобы откатить точечно, если сломает.

---

## Как проверять

После каждого коммита:

1. `dotnet build backend/post-radio.slnx --no-restore` — сборка чистая.
2. Локально: start-cluster, открыть плеер, проиграть 3-5 треков подряд, проверить:
   - реальный конец трека → переход на следующий (не обрезан);
   - кнопка Skip → следующий трек ровно один раз (не два);
   - смена плейлиста → первая песня нового плейлиста действительно играет, не скипается.
3. Chrome DevTools → Network: throttle до Slow 3G, проверить что при буферизации песня не скипается (P0.3 + P0.1 через `waiting`).
4. После P3.1 — снять browser-log, глазами пройти по decision tree.

---

## Rollback

Каждый коммит реверсится `git revert`. Если после P0 появятся неожиданные проблемы (например, нет `onended` от какого-то кривого MP3, и плеер зависает без триггера):
- временно вернуть `diff < 1`, но с порогом `duration > 5`;
- или поднять watchdog timeout до 20 сек.

---

## Чего НЕ делаем в этом плане

- Не переписываем плеер на state-machine (это долгосрочная рекомендация, не текущий фикс).
- Не трогаем backend proxy (`Server/Program.cs`) — он выглядит корректно.
- Не трогаем `OnlineTracker` — он не причём.
- Не делаем `SetLoaded` двухфазным — после диалога с Codex выяснили, что это не race.
- Не делаем отдельный abstraction layer — патчим на месте.
