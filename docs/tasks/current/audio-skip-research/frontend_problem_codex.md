# Frontend audio skip research — frontend_problem_0

Дата: 2026-04-23
Автор: Codex
Статус: read-only анализ, без фиксов

## Короткий вывод

Самое подозрительное место — `backend/Frontend/Client/Components/AudioPlayer.razor`.

Плеер сейчас не ждёт настоящий `ended` event от `<audio>`, а сам решает, что трек надо сменить, по эвристикам:

1. если до конца осталось меньше 1 секунды — сразу `SetNext()`;
2. если `ontimeupdate` не приходил 10 секунд — считается, что трек закончился/завис/сломался, и вызывается `SetNext()`;
3. если `audio.play()` вернул ошибку — вызывается `SetNext()`;
4. `SetNext()` вызывается из нескольких мест fire-and-forget и не сериализован, поэтому возможны гонки, двойные next и перескок через трек.

Наиболее вероятный механизм пользовательского симптома “иногда песня не доигрывает и просто скипается дальше”: audio element перестал присылать `timeupdate` на 10 секунд из-за buffering/stall/network/proxy/browser throttling/interop delay, после чего C# loop сам переключил трек.

---

## Ranked hypotheses

| Rank | Гипотеза | Confidence | Основание |
|---:|---|---|---|
| 1 | 10-секундный watchdog по `ontimeupdate` скипает трек при буферизации/зависании | High | `Loop()` выходит из ожидания, если `_lastTimeUpdate` старше 10 секунд, и вызывает `SetNext()` |
| 2 | Конец трека обрезается намеренно за 1 секунду | High | `OnAudioProgress()` вызывает `SetNext()`, когда `duration - currentTime < 1` |
| 3 | Гонки нескольких `SetNext()` | High as code risk / Medium as symptom | `SetNext()` вызывается fire-and-forget из progress, skip, playlist change, exception, loop; внутри есть async await и инкремент очереди |
| 4 | `audio.play()` failure превращается в skip | Medium | JS возвращает false на любую ошибку play, C# вызывает `SetNext()` |
| 5 | Stream/proxy/range/404/network issue вызывает stall, а frontend затем скипает | Medium | backend stream может зависнуть/оборваться, а frontend не различает error/waiting/stalled/ended |
| 6 | Битые/неполные mp3 на deploy volume | Low-Medium | загрузка пишет сразу в финальный mp3 без temp/atomic rename; локальные 29 mp3 проверены ffprobe/ffmpeg без ошибок |
| 7 | OnlineTracker/presence | Low | presence только touch-ит session id и не влияет напрямую на player state |

---

## Подозрительное место 1: watchdog “нет timeupdate 10 секунд => next”

Файл: `backend/Frontend/Client/Components/AudioPlayer.razor`

Ключевой код:

```csharp
private async Task Loop()
{
    var updateTimeSpan = TimeSpan.FromSeconds(10);
    ...

    while (Check())
        await Task.Delay(500, State.Token);

    if (_audioUrl == url)
        await SetNext();

    bool Check()
    {
        if (_audioUrl != url) return false;
        if (DateTime.UtcNow - _lastTimeUpdate >= updateTimeSpan) return false;
        return !State.Token.IsCancellationRequested;
    }
}
```

`_lastTimeUpdate` обновляется только из JS callback:

```csharp
[JSInvokable]
public void OnAudioProgress(double currentTime, double duration)
{
    _lastTimeUpdate = DateTime.UtcNow;
    var diff = duration - currentTime;
    if (diff < StartOverTime)
        _ = SetNext();
}
```

JS callback висит только на `ontimeupdate`:

```js
audio.ontimeupdate = () => {
    if (audio.currentTime == null || isNaN(audio.currentTime)) return;
    if (audio.duration == null || isNaN(audio.duration)) return;
    dotnetHelper.invokeMethodAsync("OnAudioProgress", audio.currentTime, audio.duration);
};
```

Почему это подозрительно:

- `timeupdate` не является надёжным “heartbeat успешного playback”.
- При buffering/stall браузер может перестать присылать `timeupdate`, хотя трек не закончился.
- При throttling вкладки, лаге WASM/Blazor, GC, CPU pause, проблемах JS interop callback тоже может задержаться.
- Через 10 секунд код без проверки `audio.ended`, `audio.paused`, `audio.error`, `readyState`, `networkState` вызывает `SetNext()`.

Пример сценария:

```text
00:00 track A started
01:20 network hiccup / buffer underrun
01:20-01:30 no timeupdate events
01:30 Check() returns false because _lastTimeUpdate older than 10s
01:30 Loop calls SetNext()
01:30 UI/player switches to track B
```

Пользователь видит: “песня не доиграла, сама скипнулась”.

---

## Подозрительное место 2: intentional early skip за 1 секунду до конца

Файл: `backend/Frontend/Client/Components/AudioPlayer.razor`

```csharp
private const double StartOverTime = 1;
...
var diff = duration - currentTime;
if (diff < StartOverTime)
    _ = SetNext();
```

Это означает, что текущая реализация принципиально не ждёт полного конца файла. Она переключается, когда до конца осталось меньше секунды.

Если пользователь говорит “не до конца” и речь про самый хвост — это не баг окружения, а прямое поведение кода.

Почему может быть хуже, чем 1 секунда:

- MP3 duration может быть неточной, особенно если VBR/header metadata странные.
- Browser может обновлять `timeupdate` крупными шагами.
- `duration` может пересчитаться после metadata/durationchange.
- Если duration меньше реальной длительности или currentTime прыгает, условие `diff < 1` сработает раньше слышимого конца.

Пример:

```text
reported duration = 180.0
actual audible content = 183.0
currentTime reaches 179.2
reported diff = 0.8
SetNext()
user loses ~3.8 seconds audible tail
```

---

## Подозрительное место 3: нет real media lifecycle events

Сейчас используются:

- `ontimeupdate` only.

Не используются:

- `onended`;
- `onerror`;
- `onwaiting`;
- `onstalled`;
- `onsuspend`;
- `onabort`;
- `onpause`;
- `onplaying`;
- `oncanplay`;
- `ondurationchange`.

Из-за этого код не различает:

```text
реальный конец трека
media decode error
network stall
buffering
browser stopped loading
source changed while play pending
empty src
range request failed
backend returned 404/500
```

Все эти состояния в текущей архитектуре легко сводятся к одному outcome: `SetNext()`.

---

## Подозрительное место 4: гонки SetNext()

Файл: `backend/Frontend/Client/Components/AudioPlayer.razor`

Источники вызова `SetNext()`:

```csharp
State.SkipRequested += () => _ = SetNext();
State.PlaylistChanged += () => _ = SetNext();
```

```csharp
if (!started)
{
    if (_audioUrl == url)
        await SetNext();
}
```

```csharp
if (_audioUrl == url)
    await SetNext();
```

```csharp
catch (Exception e)
{
    Console.WriteLine(e);
    try { await Task.Delay(TimeSpan.FromSeconds(5), State.Token); _ = SetNext(); }
    catch (OperationCanceledException) { return; }
}
```

```csharp
if (diff < StartOverTime)
    _ = SetNext();
```

Сам `SetNext()`:

```csharp
private async Task SetNext()
{
    var song = State.IncSongIndex();
    State.SetCurrentSong(song);

    if (song == null)
    {
        _audioUrl = string.Empty;
        await InvokeAsync(StateHasChanged);
        return;
    }

    _audioUrl = await Api.GetSongStreamUrl(song.Id);
    await InvokeAsync(StateHasChanged);
}
```

`IncSongIndex()`:

```csharp
public SongDto? IncSongIndex()
{
    if (_playlistSongs.Count == 0)
        return null;

    var next = _playlistSongs[_songIndex % _playlistSongs.Count];
    _songIndex++;
    return next;
}
```

Почему это подозрительно:

- `SetNext()` не защищён `SemaphoreSlim`, lock, single-flight, queue, generation token.
- `SetNext()` увеличивает `_songIndex` до async `await Api.GetSongStreamUrl(song.Id)`.
- Несколько вызовов могут одновременно выбрать разные songs.
- Последний завершившийся `GetSongStreamUrl` перезапишет `_audioUrl`.
- `CurrentSong` и `_audioUrl` могут временно или постоянно рассинхронизироваться.

Пример double-next:

```text
T0 OnAudioProgress sees diff < 1 and starts SetNext() fire-and-forget
T0+5ms Loop also exits Check() and calls SetNext()
SetNext #1: IncSongIndex => song B, awaits URL
SetNext #2: IncSongIndex => song C, awaits URL
_songIndex advanced twice
depending on await order, _audioUrl becomes B or C
user may observe skipped B or weird UI/audio mismatch
```

Пример playlist-change race:

```text
User changes playlist
SessionState.SetPlaylist loads new list and resets _songIndex = 0
PlaylistChanged fires SetNext()
Old loop/watchdog/progress from previous track also fires SetNext()
Both operate on new list or mixed current url timing
Result: first song in new playlist can be skipped immediately
```

---

## Подозрительное место 5: play() failure => skip

JS:

```js
async function play() {
    const audio = document.getElementById("audio");
    if (!audio.getAttribute("src")) return false;
    audio.currentTime = 0;
    try {
        await audio.play();
        return true;
    } catch (e) {
        if (e.name !== 'AbortError') console.error("play error:", e);
        return false;
    }
}
```

C#:

```csharp
var started = await Js.InvokeAsync<bool>("play");

if (!started)
{
    if (_audioUrl == url)
        await SetNext();

    await Task.Delay(TimeSpan.FromSeconds(1), State.Token);
    continue;
}
```

Это было добавлено commit `69679ac [Frontend] Avoid playing empty audio sources` для случая fresh deploy без загруженных песен / empty src.

Риск: любая transient ошибка `audio.play()` теперь трактуется как “надо скипнуть трек”.

Возможные ошибки:

- `NotSupportedError`;
- `AbortError` из-за быстрой смены src/stop/play;
- decode/media error;
- autoplay/browser policy edge case;
- network source not ready;
- element state race.

Особенно подозрительно вместе с `await Js.InvokeVoidAsync("stop")`, `StateHasChanged`, `Task.Delay(100)`, потом `play()`: src меняется через Blazor render, и код надеется, что через 100ms DOM уже стабилен.

---

## Подозрительное место 6: `stop()` перед каждым play сбрасывает currentTime

JS:

```js
function stop() {
    const audio = document.getElementById("audio");
    audio.pause();
    audio.currentTime = 0;
}
```

Loop:

```csharp
await Js.InvokeVoidAsync("stop");
var url = _audioUrl;
_lastTimeUpdate = DateTime.UtcNow;
await InvokeAsync(StateHasChanged);
await Task.Delay(100, State.Token);
var started = await Js.InvokeAsync<bool>("play");
```

В нормальном path это используется при старте нового URL. Но если `_audioUrl` не поменялся, а loop переходит в очередную итерацию из-за timeout/exception, `stop()` может сбросить текущую песню в начало перед попыткой play или рядом с SetNext.

Это не главная причина “skip”, но подозрительно для нестабильных состояний.

---

## Подозрительное место 7: получение stream URL через API возвращает empty string на ошибку

Файл: `backend/Frontend/Client/Services/RadioApi.cs`

```csharp
public async Task<string> GetSongStreamUrl(long id)
{
    try
    {
        return await _http.GetStringAsync(WithSession($"/api/radio/songs/{id}/stream"));
    }
    catch (Exception e)
    {
        _logger.LogWarning(e, "[RadioApi] GetSongStreamUrl failed");
        return string.Empty;
    }
}
```

Если API transient fail — `_audioUrl` становится empty. Loop затем либо пытается выбрать следующий трек, либо ждёт 2 секунды, потом снова `SetNext()`.

Пример:

```text
SetNext picks song B
GetSongStreamUrl(B) fails transiently
_audioUrl = ""
Loop sees empty url
SetNext picks song C
song B skipped without user knowing why
```

---

## Backend stream path

Файл: `backend/Orchestration/MetaGateway/RadioEndpoints.cs`

Список песен фильтруется loaded + file exists:

```csharp
return source
    .Where(kv => kv.Value.IsLoaded && File.Exists(storage.GetAudioPath(kv.Key)))
    .Select(kv => new SongDto { ... })
    .ToList();
```

Stream URL endpoint:

```csharp
private static string GetSongStream(..., long id)
{
    var sessionId = Touch(onlineTracker, context);
    return AppendSessionId(storage.GetAudioUrl(id), sessionId);
}
```

Audio file endpoint:

```csharp
private static IResult GetAudioFile(..., long id)
{
    var path = storage.GetAudioPath(id);

    if (!File.Exists(path))
        return Results.NotFound();

    Touch(onlineTracker, context);
    return Results.File(path, "audio/mpeg", enableRangeProcessing: true);
}
```

Frontend server proxy:

Файл: `backend/Frontend/Server/Program.cs`

```csharp
using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
ctx.Response.StatusCode = (int)resp.StatusCode;
foreach (var h in resp.Headers)
    ctx.Response.Headers[h.Key] = h.Value.ToArray();
foreach (var h in resp.Content.Headers)
    ctx.Response.Headers[h.Key] = h.Value.ToArray();
ctx.Response.Headers.Remove("transfer-encoding");
ctx.Response.Headers.Remove("connection");
await resp.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
```

Backend выглядит в целом нормальным: range processing включён, content type audio/mpeg, 404 если файла нет.

Но если stream завис/оборвался/range request failed/browser media error, frontend не узнаёт причину — у него нет `onerror/onstalled`. Через watchdog это превращается в skip.

---

## MediaStorage / загрузка mp3

Файл: `backend/Common/Storage/MediaStorage.cs`

```csharp
public async Task SaveAudio(long id, Stream stream)
{
    await EnsureStorage();

    var path = GetAudioPath(id);
    await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read,
        bufferSize: 1024 * 128, useAsync: true);
    await stream.CopyToAsync(file);
}
```

Файл: `backend/Meta/Audio/Playlists/PlaylistLoader.cs`

```csharp
var mediaUrl = await _soundCloud.Tracks.GetDownloadUrlAsync(state.Url);
var request = new HttpRequestMessage(HttpMethod.Get, mediaUrl);
var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
...
await using var stream = await response.Content.ReadAsStreamAsync();
await _mediaStorage.SaveAudio(id, stream);
```

После download:

```csharp
await Download(id, state);
await _orleans.GetGrain<ISong>(id).SetLoaded(true);
```

Риск:

- файл пишется сразу в итоговый `id.mp3`, без `.tmp` + atomic rename;
- нет сравнения с Content-Length;
- нет ffprobe/декод-проверки;
- если процесс умрёт в середине, может остаться неполный файл;
- если файл уже был listed как loaded ранее, frontend может получить странное media поведение.

Но локальная проверка `.media/audio`:

```text
29 mp3 files
ffprobe duration exists for all
ffmpeg decode warnings/errors: 0
```

Так что локальные файлы не подтверждают проблему. На prod volume это всё ещё надо проверить отдельно.

---

## OnlineTracker / presence

Файл: `backend/Meta/Online/OnlineTracker.cs`

`Touch()`:

```csharp
public void Touch(string? sessionId)
{
    sessionId = NormalizeSessionId(sessionId);
    if (sessionId == null)
        return;

    var now = DateTime.UtcNow;

    lock (_sync)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.LastSeenAtUtc = now;
            return;
        }

        _sessions[sessionId] = new OnlineSessionEntry
        {
            SessionId = sessionId,
            StartedAtUtc = now,
            LastSeenAtUtc = now
        };
    }
}
```

Presence touch вызывается из endpoints, включая stream/url/media. Явной связи с skip нет. Максимум — если `Touch()` стал бы тяжёлым/исключающим, но текущий код простой.

Текущий modified `OnlineTracker.cs` не выглядит причиной frontend audio skip.

---

## Что логировать для доказательства

Самое полезное — добавить reason к каждому `SetNext()`.

Примеры reason:

```text
startup-empty-url
play-failed
watchdog-timeout-no-timeupdate-10s
near-end-diff-lt-1s
loop-url-changed
manual-skip
playlist-changed
exception-delay-fallback
audio-ended-event
audio-error-event
audio-stalled-event
```

Минимальный frontend browser log для audio element:

```js
const events = [
  "loadstart", "durationchange", "loadedmetadata", "canplay", "canplaythrough",
  "play", "playing", "pause", "waiting", "stalled", "suspend", "abort",
  "error", "ended", "timeupdate", "seeking", "seeked", "emptied"
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
```

Если при проблеме перед skip будет `waiting/stalled` и потом `watchdog-timeout-no-timeupdate-10s`, гипотеза 1 подтверждена.

Если перед skip будет `near-end-diff-lt-1s`, это гипотеза 2.

Если подряд два `SetNext()` с разными reason — гипотеза 3.

Если `play-failed` — гипотеза 4.

---

## Возможные направления фикса

Не implementation plan, просто идеи:

1. Использовать `audio.onended` как основной триггер next.
2. Убрать или сильно изменить `diff < 1` early next.
3. Watchdog не должен сразу skip-ать: при timeout сначала inspect `audio.paused/ended/error/readyState/networkState`.
4. Добавить handling `waiting/stalled/error` с логированием и retry текущего трека, а не immediate next.
5. Сериализовать `SetNext()` через `SemaphoreSlim` или single-flight state machine.
6. Передавать `reason` в `SetNext(reason)` и логировать URL/song id/currentTime/duration.
7. Использовать generation token: старый async `GetSongStreamUrl` не должен перезаписывать `_audioUrl`, если уже выбран новый трек.
8. При `GetSongStreamUrl` failure не инкрементить очередь без явной причины; retry same song or mark as failed with reason.
9. При загрузке mp3 писать в temp file, проверять размер/успешность, потом atomic rename.
10. На prod проверить media files через `ffmpeg -v warning -i file -f null -`.

---

## Files referenced

- `backend/Frontend/Client/Components/AudioPlayer.razor`
- `backend/Frontend/Client/Services/SessionState.cs`
- `backend/Frontend/Client/Services/RadioApi.cs`
- `backend/Frontend/Client/Components/ControlsView.razor`
- `backend/Frontend/Client/Components/StartWait.razor`
- `backend/Orchestration/MetaGateway/RadioEndpoints.cs`
- `backend/Frontend/Server/Program.cs`
- `backend/Common/Storage/MediaStorage.cs`
- `backend/Meta/Audio/Playlists/PlaylistLoader.cs`
- `backend/Meta/Online/OnlineTracker.cs`

---

## Final gut feeling

Если надо поставить деньги на одну причину: `AudioPlayer.razor` сам скипает при отсутствии `timeupdate` 10 секунд. Это выглядит как защитный watchdog, но фактически он conflates buffering/stall with end-of-track.

Если проблема “не доигрывает самый конец” — причина ещё проще: `StartOverTime = 1`, то есть код специально переключает за секунду до reported duration.

Если иногда “перескакивает слишком далеко” — смотреть гонки `SetNext()`.
