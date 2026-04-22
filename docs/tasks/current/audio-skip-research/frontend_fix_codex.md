# Frontend audio skip — final fix plan

Дата: 2026-04-23
Статус: план исправления по итогам анализа Codex + Claude
Целевой файл: `backend/Frontend/Client/Components/AudioPlayer.razor`
Вторичные файлы: `SessionState.cs`, `RadioApi.cs`, возможно `RadioEndpoints.cs`, `MediaStorage.cs`

## Цель

Убрать случайные и преждевременные скипы треков во фронтовом плеере.

Главная проблема: текущий `AudioPlayer.razor` работает как эвристический watchdog-loop:

- скипает, если `ontimeupdate` не приходил 10 секунд;
- скипает за 1 секунду до reported duration;
- скипает при `audio.play()` failure;
- вызывает `SetNext()` из нескольких мест без сериализации;
- не слушает нормальные HTML media events: `ended`, `error`, `waiting`, `stalled`, `playing`.

Финальная модель должна быть такой:

```text
normal next:       audio ended или manual skip
error next:        explicit audio error после логирования
buffering/stall:   wait/recover, не immediate skip
progress:          только heartbeat/debug, не detector конца
SetNext:           single-flight + reason + generation token
src/play:          controlled JS loadAndPlay(), без Blazor render delay эвристики
```

---

## P0. Обязательный минимальный фикс

### 1. Ввести `SetNext(reason)` и сериализовать смену трека

Сейчас `SetNext()` вызывается параллельно из нескольких мест. Нужно сделать единый вход:

```csharp
private readonly SemaphoreSlim _setNextLock = new(1, 1);
private long _generation;

private async Task SetNext(string reason)
{
    if (!await _setNextLock.WaitAsync(0))
    {
        Console.WriteLine($"[AudioPlayer] Duplicate SetNext ignored: reason={reason}");
        return;
    }

    try
    {
        var generation = Interlocked.Increment(ref _generation);
        Console.WriteLine($"[AudioPlayer] SetNext: reason={reason}, generation={generation}");

        // выбрать следующий трек, получить URL, запустить playback
    }
    finally
    {
        _setNextLock.Release();
    }
}
```

Все текущие вызовы заменить на reason-based:

```text
State.SkipRequested       -> SetNext("manual-skip")
State.PlaylistChanged     -> SetNext("playlist-changed")
OnAudioEnded              -> SetNext("audio-ended")
OnAudioError              -> SetNext("audio-error:<code>")
play failed               -> SetNext("play-failed") или retry current first
watchdog timeout fallback -> SetNext("watchdog-timeout") только после guards
catch                     -> controlled recovery, не fire-and-forget
```

Критерий готовности:

- нет `_ = SetNext()` без reason;
- нет параллельного инкремента `_songIndex`;
- duplicate calls логируются и не двигают очередь.

---

### 2. Убрать `diff < 1` как основной detector конца трека

Сейчас:

```csharp
var diff = duration - currentTime;
if (diff < StartOverTime)
    _ = SetNext();
```

Это надо убрать из normal path.

`OnAudioProgress` должен только обновлять heartbeat/debug state:

```csharp
[JSInvokable]
public void OnAudioProgress(long generation, double currentTime, double duration)
{
    if (generation != _generation)
        return;

    _lastTimeUpdate = DateTime.UtcNow;
    _lastCurrentTime = currentTime;
    _lastDuration = duration;
}
```

Нормальное окончание трека должно идти через `audio.onended`.

Критерий готовности:

- трек не переключается по `duration - currentTime < 1`;
- последний хвост трека доигрывается до `ended`.

---

### 3. Добавить JS media events

В `audioHelper.setAudioElement` добавить обработчики:

```js
audio.onended = () => dotnetHelper.invokeMethodAsync("OnAudioEnded", window.audioHelper.generation);
audio.onerror = () => dotnetHelper.invokeMethodAsync(
  "OnAudioError",
  window.audioHelper.generation,
  audio.error ? audio.error.code : 0,
  audio.error ? audio.error.message : ""
);
audio.onwaiting = () => dotnetHelper.invokeMethodAsync("OnAudioBuffering", window.audioHelper.generation, true, "waiting");
audio.onstalled = () => dotnetHelper.invokeMethodAsync("OnAudioBuffering", window.audioHelper.generation, true, "stalled");
audio.onplaying = () => dotnetHelper.invokeMethodAsync("OnAudioBuffering", window.audioHelper.generation, false, "playing");
audio.oncanplay = () => dotnetHelper.invokeMethodAsync("OnAudioCanPlay", window.audioHelper.generation);
audio.ondurationchange = () => dotnetHelper.invokeMethodAsync("OnAudioDurationChange", window.audioHelper.generation, audio.duration);
audio.onabort = () => dotnetHelper.invokeMethodAsync("OnAudioAbort", window.audioHelper.generation);
audio.onemptied = () => dotnetHelper.invokeMethodAsync("OnAudioEmptied", window.audioHelper.generation);
```

Минимально обязательные:

```text
ended
error
waiting
stalled
playing
timeupdate
```

Критерий готовности:

- реальные причины переходов видны в console/log;
- buffering не выглядит как конец трека;
- audio error не маскируется 10-секундным watchdog-ом.

---

### 4. Добавить generation token для всех JS callbacks

Проблема: `OnAudioProgress` от старого трека может доехать после смены `_audioUrl` и вызвать ложный next или обновить heartbeat новому треку.

Решение:

- при каждой смене трека C# увеличивает `_generation`;
- JS хранит текущую generation;
- все callbacks возвращают generation;
- C# игнорирует stale generation.

Пример:

```js
window.audioHelper.currentGeneration = 0;

window.audioHelper.loadAndPlay = async (url, generation) => {
  window.audioHelper.currentGeneration = generation;
  const audio = document.getElementById("audio");
  audio.pause();
  audio.src = url;
  audio.load();
  await audio.play();
  return true;
};
```

```csharp
private bool IsCurrentGeneration(long generation) => generation == _generation;
```

Критерий готовности:

- stale `timeupdate`, `ended`, `error` от старого трека не меняют состояние текущего трека.

---

### 5. Переделать watchdog: не skip-ать во время buffering/stalled

Сейчас watchdog делает next, если нет `timeupdate` 10 секунд.

Новая логика:

```csharp
private bool _isBuffering;
private DateTime _bufferingStartedAt;
private DateTime _lastTimeUpdate;

bool ShouldWatchdogSkip()
{
    if (_isBuffering)
    {
        // Не скипать быстрым 10s timeout. Можно иметь long timeout 60-120s.
        return DateTime.UtcNow - _bufferingStartedAt > TimeSpan.FromSeconds(90);
    }

    return DateTime.UtcNow - _lastTimeUpdate > TimeSpan.FromSeconds(30);
}
```

Рекомендация:

- short timeout 10s убрать;
- normal watchdog сделать 30s+;
- buffering watchdog сделать 90s+;
- при timeout логировать `currentTime`, `duration`, `readyState`, `networkState`, `buffering`, `src`, `generation`.

Критерий готовности:

- кратковременный network/buffer stall не скипает трек;
- timeout имеет явный reason и диагностику.

---

## P1. Стабилизация загрузки и play

### 6. Заменить Blazor `src` binding + `Delay(100)` на JS `loadAndPlay(url, generation)`

Сейчас:

```csharp
_audioUrl = await Api.GetSongStreamUrl(song.Id);
await InvokeAsync(StateHasChanged);
await Task.Delay(100, State.Token);
var started = await Js.InvokeAsync<bool>("play");
```

Проблемы:

- `StateHasChanged` не гарантирует, что DOM уже обновлён;
- `100ms` — эвристика;
- `_lastTimeUpdate` стартует до фактического play;
- нет явного `audio.load()`;
- можно случайно играть старый src.

Нужно:

```js
async function loadAndPlay(url, generation) {
  const audio = document.getElementById("audio");
  window.audioHelper.currentGeneration = generation;

  audio.pause();
  audio.removeAttribute("src");
  audio.load();

  audio.src = url;
  audio.load();

  try {
    await audio.play();
    return true;
  } catch (e) {
    if (e.name !== "AbortError") console.error("play error:", e);
    return false;
  }
}
```

C#:

```csharp
var started = await Js.InvokeAsync<bool>("audioHelper.loadAndPlay", url, generation);
```

`_lastTimeUpdate` обновлять после `playing` event или успешного `play()`.

Критерий готовности:

- нет зависимости от Blazor render timing для смены audio src;
- `audio.load()` вызывается явно;
- play запускается ровно для URL выбранного generation.

---

### 7. Не инкрементить очередь окончательно до успешного URL/play decision

Сейчас `SetNext()` сначала двигает индекс и UI, потом получает URL:

```csharp
var song = State.IncSongIndex();
State.SetCurrentSong(song);
_audioUrl = await Api.GetSongStreamUrl(song.Id);
```

Если URL получить не удалось, песня уже пропущена.

Варианты:

#### Вариант A — минимальный

Оставить `IncSongIndex`, но если URL пустой:

- логировать `stream-url-empty`;
- retry same song 1-3 раза;
- только потом skip с reason `stream-url-failed`.

#### Вариант B — лучше

Добавить в `SessionState` методы:

```csharp
SongDto? PeekNextSong();
void CommitNextSong(SongDto song);
```

Тогда:

1. peek candidate;
2. получить URL;
3. если URL ok — commit current song/index;
4. если URL fail — retry или mark failed без silent skip.

Критерий готовности:

- transient `/songs/{id}/stream` failure не молча пропускает песню;
- UI `CurrentSong` не показывает песню без валидного URL/play attempt.

---

### 8. Полноценный Dispose и event unsubscribe

Сейчас:

```csharp
public void Dispose() => _dotNetRef?.Dispose();
```

Нужно:

- сохранить handlers в поля;
- отписаться от `State.Started`, `State.SkipRequested`, `State.PlaylistChanged`, `State.VolumeChanged`;
- иметь локальный `CancellationTokenSource` для loop;
- отменять loop в Dispose;
- dispose `DotNetObjectReference`.

Пример:

```csharp
private CancellationTokenSource? _componentCts;
private Action? _startedHandler;
private Action? _skipHandler;
private Action? _playlistChangedHandler;
private Action? _volumeChangedHandler;

protected override void OnInitialized()
{
    _componentCts = CancellationTokenSource.CreateLinkedTokenSource(State.Token);

    _startedHandler = () => _ = Loop(_componentCts.Token);
    _skipHandler = () => _ = SetNext("manual-skip");
    _playlistChangedHandler = () => _ = SetNext("playlist-changed");
    _volumeChangedHandler = () => _ = OnVolumeChanged(State.Volume);

    State.Started += _startedHandler;
    State.SkipRequested += _skipHandler;
    State.PlaylistChanged += _playlistChangedHandler;
    State.VolumeChanged += _volumeChangedHandler;
}

public void Dispose()
{
    if (_startedHandler != null) State.Started -= _startedHandler;
    if (_skipHandler != null) State.SkipRequested -= _skipHandler;
    if (_playlistChangedHandler != null) State.PlaylistChanged -= _playlistChangedHandler;
    if (_volumeChangedHandler != null) State.VolumeChanged -= _volumeChangedHandler;

    _componentCts?.Cancel();
    _componentCts?.Dispose();
    _dotNetRef?.Dispose();
}
```

Критерий готовности:

- после navigation/hot reload не остаётся старых loops;
- нет нескольких active handlers на один `SessionState`.

---

### 9. Убрать fire-and-forget из exception path

Сейчас:

```csharp
catch (Exception e)
{
    Console.WriteLine(e);
    try { await Task.Delay(TimeSpan.FromSeconds(5), State.Token); _ = SetNext(); }
    catch (OperationCanceledException) { return; }
}
```

Нужно:

```csharp
catch (Exception e)
{
    Console.WriteLine($"[AudioPlayer] Loop exception: {e}");
    try
    {
        await Task.Delay(TimeSpan.FromSeconds(5), token);
        await SetNext("loop-exception");
    }
    catch (OperationCanceledException)
    {
        return;
    }
}
```

Но лучше: exception path не должен всегда next. Если exception от JS interop/dispose — выйти. Если transient play failure — retry current.

Критерий готовности:

- no `_ = SetNext()` in catch;
- exceptions не теряются;
- reason логируется.

---

## P2. Backend hardening

### 10. `GetSongStream` должен проверять наличие файла

Сейчас:

```csharp
private static string GetSongStream(..., long id)
{
    var sessionId = Touch(onlineTracker, context);
    return AppendSessionId(storage.GetAudioUrl(id), sessionId);
}
```

Лучше:

```csharp
private static IResult GetSongStream(..., long id)
{
    var path = storage.GetAudioPath(id);
    if (!File.Exists(path))
        return Results.NotFound();

    var sessionId = Touch(onlineTracker, context);
    return Results.Text(AppendSessionId(storage.GetAudioUrl(id), sessionId));
}
```

Критерий готовности:

- frontend получает явный 404 на этапе URL request;
- не стартует `<audio>` с URL, который точно отсутствует.

---

### 11. Atomic mp3 write

Сейчас файл пишется сразу в финальный путь:

```csharp
var path = GetAudioPath(id);
await using var file = new FileStream(path, FileMode.Create, ...);
await stream.CopyToAsync(file);
```

Лучше:

```text
{id}.mp3.tmp -> flush/close -> move/replace -> {id}.mp3
```

Это не главный frontend root cause, но снижает риск partial mp3 на prod volume.

Критерий готовности:

- frontend никогда не увидит partially-written final mp3;
- `IsLoaded=true` соответствует существующему завершённому файлу.

---

## Диагностика после фикса

### Runtime logs

Каждый переход должен писать:

```text
[AudioPlayer] SetNext reason=audio-ended generation=12 songId=... url=...
[AudioPlayer] Buffering started reason=waiting generation=12 ct=... duration=...
[AudioPlayer] Buffering ended reason=playing generation=12
[AudioPlayer] Audio error generation=12 code=... message=...
[AudioPlayer] Watchdog timeout generation=12 buffering=false ct=... duration=... readyState=... networkState=...
[AudioPlayer] Ignored stale event event=timeupdate eventGeneration=11 currentGeneration=12
[AudioPlayer] Duplicate SetNext ignored reason=audio-ended currentReason=manual-skip
```

### Browser check

Проверить в Chrome:

```text
chrome://media-internals
```

Во время скипа смотреть:

- `ended` был или нет;
- `pipeline_error` / media error;
- `stalled/waiting`;
- network state;
- duration/currentTime.

### Manual cases

Проверить:

1. обычное проигрывание трека до конца;
2. ручной skip;
3. смена playlist во время playback;
4. временное отключение сети / throttling;
5. backend returns 404 for one audio file;
6. быстро нажать skip несколько раз;
7. navigation away/back;
8. background tab на 1-2 минуты.

---

## Рекомендуемый порядок реализации

1. В `AudioPlayer.razor` добавить `SetNext(reason)` + semaphore + logging.
2. Добавить generation token.
3. Добавить JS media events: `ended/error/waiting/stalled/playing/timeupdate`.
4. Перевести normal next на `OnAudioEnded`.
5. Убрать `diff < 1` next из `OnAudioProgress`.
6. Переделать watchdog: не skip during buffering, увеличить timeout, добавить diagnostics.
7. Заменить Blazor `src` binding flow на JS `loadAndPlay(url, generation)` с `audio.load()`.
8. Исправить Dispose/unsubscribe/component CTS.
9. Исправить catch path: no fire-and-forget.
10. Улучшить `RadioApi.GetSongStreamUrl`: no silent empty skip; retry/log.
11. Опционально backend: `GetSongStream` file check + atomic mp3 write.

---

## Definition of Done

- Песня доигрывает до `ended`, а не до `duration - currentTime < 1`.
- Кратковременный `waiting/stalled` не вызывает skip через 10 секунд.
- Любой skip имеет явный reason в логах.
- Быстрые двойные события не инкрементят `_songIndex` дважды.
- Stale JS callbacks игнорируются по generation token.
- При смене трека JS явно делает `src`, `load()`, `play()` для нужной generation.
- Dispose отменяет loop и отписывает handlers.
- Ошибка URL/media не превращается в silent skip без логов/retry.
