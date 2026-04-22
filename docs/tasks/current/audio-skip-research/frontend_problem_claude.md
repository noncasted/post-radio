● Audio Skip Research

Расследование бага: на Blazor-фронте пост-радио иногда песня не доигрывает до конца и сама скипается дальше.

Репозиторий: /projects/post-radio. Стек: .NET 10 + Orleans backend + Blazor WebAssembly Client + HTML <audio>.

  ---
1. Архитектура воспроизведения

Единственная точка управления воспроизведением — backend/Frontend/Client/Components/AudioPlayer.razor. Вся клиентская логика сосредоточена там.

Жизненный цикл трека

Loop() (строки 64-126)
|-- if (string.IsNullOrWhiteSpace(_audioUrl)) SetNext()
|-- if (string.IsNullOrWhiteSpace(_audioUrl)) { Delay(2s); continue; }
|-- InvokeVoidAsync("stop")            # audio.pause() + currentTime=0
|-- url = _audioUrl                    # snapshot для race-check
|-- _lastTimeUpdate = UtcNow           # старт watchdog
|-- StateHasChanged + Delay(100ms)     # ждём Blazor render нового <audio src>
|-- started = InvokeAsync<bool>("play")
|-- if (!started) { if (_audioUrl == url) SetNext(); Delay(1s); continue; }
|-- while (Check()) Delay(500ms)
|     Check() == false если:
|       - _audioUrl != url (другой источник сменил трек)
|       - UtcNow - _lastTimeUpdate >= 10s (watchdog)
|       - State.Token отменён
|-- if (_audioUrl == url) SetNext()

Параллельные триггеры смены трека

В OnInitialized (строки 56-62):

State.Started += () => _ = Loop();
State.SkipRequested += () => _ = SetNext();
State.PlaylistChanged += () => _ = SetNext();
State.VolumeChanged += () => _ = OnVolumeChanged(State.Volume);

Плюс OnAudioProgress (строки 137-144) — JSInvokable callback из JS-handler audio.ontimeupdate:

[JSInvokable]
public void OnAudioProgress(double currentTime, double duration)
{
_lastTimeUpdate = DateTime.UtcNow;
var diff = duration - currentTime;
if (diff < StartOverTime)   // StartOverTime = 1
_ = SetNext();
}

Плюс catch в Loop (строки 119-124) тоже планирует SetNext() после 5-секундной паузы.

SetNext (строки 146-160)

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

SessionState.IncSongIndex (SessionState.cs:86-94)

public SongDto? IncSongIndex()
{
if (_playlistSongs.Count == 0)
return null;

      var next = _playlistSongs[_songIndex % _playlistSongs.Count];
      _songIndex++;
      return next;
}

Ни семафора, ни lock — просто инкремент поля.

JS-слой (AudioPlayer.razor:9-45)

window.audioHelper = {
setAudioElement: (dotnetHelper) => {
const audio = document.getElementById("audio");
audio.ontimeupdate = () => {
if (audio.currentTime == null || isNaN(audio.currentTime)) return;
if (audio.duration == null || isNaN(audio.duration)) return;
dotnetHelper.invokeMethodAsync("OnAudioProgress", audio.currentTime, audio.duration);
};
},
};

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

function stop() {
const audio = document.getElementById("audio");
audio.pause();
audio.currentTime = 0;
}

Обратите внимание: единственный HTML5 media event, который слушает плеер, — ontimeupdate. Нет ни error, ни stalled, ни waiting, ни suspend, ни abort,
ни ended, ни loadedmetadata, ни canplay, ни emptied.

Backend endpoints (RadioEndpoints.cs)

- /api/radio/songs/{id}/stream (строки 85-93): возвращает строку URL /api/radio/media/audio/{id}, файл не проверяет.
- /api/radio/media/audio/{id} (строки 95-108): Results.File(path, "audio/mpeg", enableRangeProcessing: true) либо Results.NotFound().
- /api/radio/songs (строки 59-83): фильтрует IsLoaded && File.Exists(storage.GetAudioPath(kv.Key)) — OK, но это снимок на момент листинга, к моменту
  реального /stream файл может исчезнуть.

RadioApi.GetSongStreamUrl (строки 78-89)

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

Любой сетевой сбой молча превращается в пустую строку.

  ---
2. Точки потенциального скипа

[CRITICAL #1] Watchdog на 10 секунд скипает при буфер-андерране

Файл: AudioPlayer.razor:67, 102-113

var updateTimeSpan = TimeSpan.FromSeconds(10);
...
while (Check()) await Task.Delay(500, State.Token);
if (_audioUrl == url) await SetNext();

bool Check()
{
if (_audioUrl != url) return false;
if (DateTime.UtcNow - _lastTimeUpdate >= updateTimeSpan) return false;
return !State.Token.IsCancellationRequested;
}

Суть: ontimeupdate срабатывает ТОЛЬКО когда currentTime реально двигается. Если воспроизведение остановилось — событие замолкает. Плеер трактует тишину
как «зависли» и скипает.

Когда событие замолкает, а песня НЕ кончилась:

1. Буфер-андерран: сеть подвисла → <audio> внутренне переходит в readyState = HAVE_CURRENT_DATA, ставит networkState = NETWORK_LOADING, ждёт данные;
   ontimeupdate не шлётся.
2. Browser tab throttling: Chromium троттлит Task.Delay и таймеры в неактивных табах (до 1 тика в минуту через пару минут в фоне). Firefox может
   полностью приостанавливать <audio>-элементы. Вернулся к табу → UtcNow - _lastTimeUpdate давно > 10 сек → мгновенный скип.
3. Медленный/проксируемый SoundCloud: в docs/DEPLOY.md описан SOCKS5-прокси для media. Любая икота ≥10 сек → скип.
4. Ошибка декодирования середины MP3 (битый фрейм посреди трека): браузер молча прекращает playback, ontimeupdate замолкает.
5. stalled event: браузер сам его шлёт, когда данных не хватает, но плеер его не ловит.

Симптом: «трек играл нормально минуту, вдруг посреди песни скип на следующий».

Исторический контекст: коммит ded49cd (ноябрь 2025) поднял таймаут с 5 до 10 сек — явно по той же симптоматике. 10 тоже часто мало.

  ---
[CRITICAL #2] OnAudioProgress скипает при кривом duration

Файл: AudioPlayer.razor:137-144

private const double StartOverTime = 1;
...
public void OnAudioProgress(double currentTime, double duration)
{
_lastTimeUpdate = DateTime.UtcNow;
var diff = duration - currentTime;
if (diff < StartOverTime)
_ = SetNext();
}

JS-фильтр (строки 13-17) отбрасывает только null/NaN. Но duration может быть:

1. Infinity (chunked без Content-Length) — diff = Infinity, не триггерит, OK.
2. Занижённым на первых timeupdate для VBR-MP3 без Xing header — браузер считает длительность по bitrate от первого прочитанного фрейма, потом
   уточняет. Пока уточняется, duration может быть близко к currentTime.
3. Стабильно неверным при битых MP3-заголовках — Chromium знаменит этим багом для VBR без Xing/VBRI.
4. Коротко-меняющимся при range-request (enableRangeProcessing: true): браузер делает chunked-load и может перевычислять duration.

Нет защиты duration > MinTrackLength и нет гейтинга через loadedmetadata — любое промежуточное значение duration < currentTime + 1 триггерит скип.

Симптом: «трек вообще не стартует — сразу следующий», проявляется выборочно на конкретных файлах.

  ---
[CRITICAL #3] Нет обработчика error у <audio>

Файл: AudioPlayer.razor:10-19

setAudioElement: (dotnetHelper) => {
const audio = document.getElementById("audio");
audio.ontimeupdate = () => { ... };
},

Отсутствуют: error, stalled, waiting, suspend, ended, loadedmetadata, canplay, emptied, abort.

Последствия:

1. 404/500 от /media/audio/{id} → <audio> emits error → плеер не знает → ontimeupdate не придёт → через 10 с watchdog скипает → пытается следующую →
   опять 404 → быстрая серия скипов по всему плейлисту.
2. stalled/waiting (истинный буфер-андерран) нельзя отличить от зависания → watchdog убивает легитимную паузу на буферизацию.
3. Полное отсутствие ended означает, что естественный конец трека ловится эвристически через diff < 1 (см. #2), а не надёжно.

404 особенно вероятен из-за race-window в PlaylistLoader: SetLoaded(true) устанавливается до того, как запись на диск физически завершена; плюс
ListSongs (RadioEndpoints.cs:71-72) фильтрует на момент листинга, но к моменту реального /stream файл может исчезнуть (retention, перезаливка, ручная
очистка).

  ---
[HIGH #4] SetNext не сериализован — гонка на _songIndex

Файл: AudioPlayer.razor:58-61 + SessionState.cs:86-94

Источники SetNext():

1. Основной Loop после выхода из while (Check()) (естественный конец или watchdog).
2. OnAudioProgress когда diff < 1.
3. SkipRequested из ControlsView (кнопка Skip).
4. PlaylistChanged при смене плейлиста.
5. catch в Loop при exception.
6. !started в Loop после неудачного play().

Blazor WASM — однопоточный, но async-continuations чередуются: пока один SetNext ждёт await Api.GetSongStreamUrl(...) (сетевой запрос, сотни мс),
второй успевает войти и тоже сделать IncSongIndex.

// SessionState.cs:86-94 — нет lock, нет SemaphoreSlim
public SongDto? IncSongIndex()
{
if (_playlistSongs.Count == 0) return null;
var next = _playlistSongs[_songIndex % _playlistSongs.Count];
_songIndex++;
return next;
}

Итог: индекс +2 → пропуск трека.

Особо опасные комбинации:
- OnAudioProgress(diff<1) + watchdog стреляют почти одновременно
- OnAudioProgress(diff<1) + кнопка Skip в последнюю секунду трека
- PlaylistChanged + ещё-активный Loop на старом плейлисте

Симптом: «перепрыгнуло ЧЕРЕЗ песню», «скипнулись сразу две подряд».

  ---
[HIGH #5] GetSongStreamUrl молча возвращает ""

Файл: RadioApi.cs:78-89

try { return await _http.GetStringAsync(...); }
catch (Exception e)
{
_logger.LogWarning(...);
return string.Empty;
}

Любая временная недоступность backend'а (Traefik перезагружается 5 сек, Kestrel-перегрузка, network blip, PgBouncer упал) → _audioUrl = "" → следующая
итерация Loop видит пустой URL (строки 76-78) → снова SetNext() → ещё один инкремент индекса.

State.SetCurrentSong(song) уже обновил label-отображение, так что пользователь видит имя трека, а стрима нет — через секунды имя меняется на следующий.

Симптом: «вижу имя песни, но она не играет, через пару секунд сменилось на следующее».

  ---
[HIGH #6] Гонка рендер-цикла с play()

Файл: AudioPlayer.razor:85-91

await Js.InvokeVoidAsync("stop");
var url = _audioUrl;
_lastTimeUpdate = DateTime.UtcNow;          // watchdog стартует СЕЙЧАС
await InvokeAsync(StateHasChanged);         // ставит rerender в очередь
await Task.Delay(100, State.Token);         // эвристика
var started = await Js.InvokeAsync<bool>("play");

Проблемы:

1. StateHasChanged не гарантирует что DOM обновлён — он ставит задачу в очередь SynchronizationContext. На медленном девайсе / при перегруженном
   рендере 100 мс недостаточно → <audio src> ещё старый → play() запускает старый трек → тот почти сразу отдаёт ontimeupdate с diff < 1 (currentTime уже
   близко к duration) → моментальный SetNext.
2. _lastTimeUpdate стартует ДО play(). Сам play() — это JS-interop через WASM boundary + preload audio → может занимать 2-3 секунды. Реальное окно до
   watchdog-скипа — не 10, а ~7 сек.
3. audio.play() Promise-resolve означает только «началось проигрывание одного буфера». Если после старта буфер моментально исчерпан (HAVE_FUTURE_DATA →
   HAVE_CURRENT_DATA), play() уже вернул true, но ontimeupdate не сработает → watchdog через 10 сек.
4. audio.load() не вызывается явно при смене src. В некоторых браузерах смена атрибута src через Blazor attribute binding не гарантированно триггерит
   перезагрузку (особенно если старый src был тот же или браузер агрессивно кэширует).

  ---
[MEDIUM #7] Dispose не отменяет Loop и не отписывается от событий

Файл: AudioPlayer.razor:162

public void Dispose() => _dotNetRef?.Dispose();

Нет:
- отписки от State.Started, State.SkipRequested, State.PlaylistChanged, State.VolumeChanged
- отмены внутреннего цикла (у Loop нет отдельного CancellationTokenSource, он зависит от State.Token, который живёт вместе с сессией)

Последствия при navigation / hot reload / редкой перерисовке родителя:

1. Новый AudioPlayer создаётся, подписывается на State.Started.
2. Старый Dispose-ится, но его Loop всё ещё вращается — работает со своим закрытым url-snapshot и вызывает SetNext(), когда видит расхождение.
3. Два активных Loop на один <audio id="audio">. Они перетирают _audioUrl друг у друга и оба дёргают SetNext().

Это хорошо стыкуется с жалобой «ИНОГДА скипает» — проявляется только после определённых navigation-паттернов.

  ---
[MEDIUM #8] Shuffle не защищён от потери треков при гонке

Файл: SessionState.cs:71-78, 86-94

public async Task SetPlaylist(PlaylistDto playlist)
{
Playlist = playlist;
_playlistSongs = (await _api.GetSongs(playlist.Id)).ToList();
Shuffle(_playlistSongs);
_songIndex = 0;
PlaylistChanged?.Invoke();
}

Shuffle выполняется один раз при смене плейлиста. Если _songIndex «перепрыгнул» из-за гонки (#4/#5), шаффл не восстановит — треки просто пропущены до
следующего цикла через playlist.

Плюс: между _playlistSongs = ... и Shuffle(...) состояние наблюдаемо из async-continuations других SetNext(), которые уже в полёте → теоретически могут
обратиться к _playlistSongs[_songIndex % count] до шаффла. Редко, но возможно при быстром переключении плейлистов.

  ---
[MEDIUM #9] OnAudioProgress может дойти после смены трека

JS-event ontimeupdate ставится в очередь JS event loop; invokeMethodAsync("OnAudioProgress", ...) идёт через WASM-boundary и попадает в Blazor
SynchronizationContext асинхронно. Между событием и обработчиком успевает произойти:

1. Loop перешёл к следующему треку, _audioUrl сменился.
2. OnAudioProgress добегает с currentTime/duration от СТАРОГО трека.
3. _lastTimeUpdate = UtcNow обновляет watchdog (маскирует реальную проблему нового трека).
4. Если старый трек закончился с diff < 1 — вызывается ещё один SetNext(), уже когда новый трек только что стартовал.

Нет seq/generation-токена у OnAudioProgress, чтобы отбрасывать устаревшие события.

  ---
[LOW #10] stop() не сбрасывает src

Файл: AudioPlayer.razor:34-38

function stop() {
const audio = document.getElementById("audio");
audio.pause();
audio.currentTime = 0;
}

При stop() сразу перед сменой src через Blazor, браузер может отменить текущую загрузку (fetch abort). Отменённая загрузка ещё может успеть доставить
error (не обработан) или ontimeupdate (обновит _lastTimeUpdate новому треку, маскируя проблему).

Правильнее: audio.pause(); audio.removeAttribute('src'); audio.load();.

  ---
[LOW #11] Catch в Loop при exception планирует SetNext через 5 сек

Файл: AudioPlayer.razor:119-124

catch (Exception e)
{
Console.WriteLine(e);
try { await Task.Delay(TimeSpan.FromSeconds(5), State.Token); _ = SetNext(); }
catch (OperationCanceledException) { return; }
}

Любое непредвиденное исключение (в том числе из JS-interop — WebAssembly может бросать при DOM-гонках) сразу инкрементирует индекс. 5 секунд паузы не
помогают восстановиться, если корневая причина — backend или сеть.

Плюс _ = SetNext() внутри catch — fire-and-forget, его собственные исключения потеряются и могут ещё раз свалиться в UnhandledException.

  ---
3. Что изменил коммит 69679ac (и какой остался риск)

Коммит 69679ac [Frontend] Avoid playing empty audio sources (Apr 22 2026).

Добавил

- Проверку пустого src в JS-play() → return false.
- Pre-play проверку string.IsNullOrWhiteSpace(_audioUrl) → SetNext() + Delay(2s); continue;.
- Retry при !started → SetNext() + Delay(1s); continue;.
- play() теперь возвращает bool вместо void.

Исправил

- Spam NotSupportedError на свежем Coolify-деплое, когда плейлисты ещё не прогрузились и URL пустой.

НЕ исправил и частично усугубил

1. При !started вызывается ещё один SetNext() (строки 95-96). Одна «не-запустившаяся» песня стоит двух инкрементов индекса, если параллельно тоже
   стрельнет event-driven SetNext (из SkipRequested, OnAudioProgress и т.п.).
2. _lastTimeUpdate = DateTime.UtcNow переехал перед play() (до коммита был после StateHasChanged, но до Task.Delay(100)) — watchdog стартует до
   фактического старта проигрывания (см. CRITICAL #6).
3. Появился новый failure-path с Task.Delay(TimeSpan.FromSeconds(1), State.Token); continue; — при временной 5xx/network blip все треки плейлиста могут
   пролететь с интервалом ~2-3 сек, безмолвно инкрементируя индекс.

Что упущено полностью

- Сериализация SetNext.
- Обработчики error/stalled/waiting/ended.
- Защита от кривого duration в OnAudioProgress.
- Правильный Dispose с отпиской от событий и отменой Loop.

  ---
4. Ранжированные гипотезы

┌─────┬─────────────┬─────────────────────────────────────────────────┬───────────────────────────────┬────────────────────────────────────────────┐
│  #  │ Вероятность │                    Сценарий                     │         Точное место          │                  Починка                   │
├─────┼─────────────┼─────────────────────────────────────────────────┼───────────────────────────────┼────────────────────────────────────────────┤
│     │             │ Watchdog 10 с скипает при буфер-андерране       │                               │ Обработчики stalled/waiting + пауза        │
│ 1   │ ~70%        │ (проксируемый SoundCloud / tab throttling /     │ AudioPlayer.razor:67, 108-113 │ watchdog на время буферизации + отдельный  │
│     │             │ медленная сеть). Плеер путает «ждём данные» с   │                               │ ended-handler вместо diff < 1              │
│     │             │ «зависли».                                      │                               │                                            │
├─────┼─────────────┼─────────────────────────────────────────────────┼───────────────────────────────┼────────────────────────────────────────────┤
│     │             │ Нет audio.onerror → 404/500 от                  │                               │ Обработчик error + явный skip + детальное  │
│ 2   │ ~55%        │ /media/audio/{id} → через 10 с watchdog →       │ AudioPlayer.razor:10-19,      │ логирование + проверка File.Exists в       │
│     │             │ каскад скипов. Особенно при race                │ RadioEndpoints.cs:103         │ GetSongStream перед возвратом URL          │
│     │             │ SetLoaded(true) vs запись файла.                │                               │                                            │
├─────┼─────────────┼─────────────────────────────────────────────────┼───────────────────────────────┼────────────────────────────────────────────┤
│     │             │ Гонка SetNext() между OnAudioProgress(diff<1),  │                               │ Сериализовать SetNext() через              │
│ 3   │ ~40%        │ watchdog, Skip-кнопкой и PlaylistChanged —      │ AudioPlayer.razor:58-61,      │ SemaphoreSlim(1,1) или Interlocked-guard   │
│     │             │ _songIndex инкрементируется 2+ раз.             │ SessionState.cs:86-94         │ («одна активная задача») + seq-токен в     │
│     │             │                                                 │                               │ OnAudioProgress                            │
├─────┼─────────────┼─────────────────────────────────────────────────┼───────────────────────────────┼────────────────────────────────────────────┤
│     │             │ duration занижено/неточно на старте VBR-MP3 или │                               │ Гейтинг через loadedmetadata + if          │
│ 4   │ ~35%        │  при chunked range-request → diff < 1 на первом │ AudioPlayer.razor:137-144     │ (duration < 2 || currentTime < 0.5)        │
│     │             │  же ontimeupdate → мгновенный скип.             │                               │ return;                                    │
├─────┼─────────────┼─────────────────────────────────────────────────┼───────────────────────────────┼────────────────────────────────────────────┤
│     │             │ Двойной Loop() после неправильного Dispose +    │                               │ CancellationTokenSource на уровне          │
│ 5   │ ~25%        │ navigation — конкурирующие циклы перетирают     │ AudioPlayer.razor:56-62, 162  │ компонента, отписки в Dispose, проверка    │
│     │             │ _audioUrl.                                      │                               │ !IsDisposed в обработчиках                 │
├─────┼─────────────┼─────────────────────────────────────────────────┼───────────────────────────────┼────────────────────────────────────────────┤
│     │             │ Blazor ещё не обновил <audio src> к моменту     │                               │ Императивная установка src из JS +         │
│ 6   │ ~20%        │ play() — запускается старый трек, быстро        │ AudioPlayer.razor:85-91       │ ожидание canplay перед play()              │
│     │             │ кончается → скип.                               │                               │                                            │
├─────┼─────────────┼─────────────────────────────────────────────────┼───────────────────────────────┼────────────────────────────────────────────┤
│     │             │ OnAudioProgress с события от УЖЕ сменившегося   │                               │ seq/generation-токен при каждой смене url; │
│ 7   │ ~15%        │ трека сбивает watchdog или триггерит лишний     │ AudioPlayer.razor:137-144     │  отбрасывать события с устаревшим seq      │
│     │             │ SetNext.                                        │                               │                                            │
└─────┴─────────────┴─────────────────────────────────────────────────┴───────────────────────────────┴────────────────────────────────────────────┘

Гипотезы 1 и 2 почти наверняка присутствуют одновременно — они вместе объясняют и «скип посреди трека», и «каскадный скип».

  ---
5. Что проверить первым в проде

Диагностика

1. Chrome DevTools → chrome://media-internals при воспроизведении. Поймать момент скипа — там видны реальные события <audio>: waiting, stalled, error,
   suspend, ended. Если там есть error с конкретным кодом или stalled за секунду до скипа — гипотеза подтверждена.
2. Временное логирование в Loop():
   Console.WriteLine($"[AudioPlayer] Check exited: url_changed={_audioUrl != url}, watchdog={DateTime.UtcNow - _lastTimeUpdate >= updateTimeSpan},
   cancelled={State.Token.IsCancellationRequested}");
2. И в OnAudioProgress:
   Console.WriteLine($"[AudioPlayer] Progress: ct={currentTime}, d={duration}, diff={duration - currentTime}");
2. За один прогон в продакшене скажет, какая гипотеза в реальности доминирует.
3. Проверить backend-логи: docker compose logs backend | grep -E "GetSongStream|GetAudioFile|404|500" на момент скипов. Если есть 404 — гипотеза #2.
4. Проверить race SetLoaded(true) vs флуш файла: git grep -n "SetLoaded" backend/Meta/Audio/Playlists/. Если SetLoaded(true) вызывается до await на
   запись файла — race подтверждён.

Что добавить в DevTools Network

Скорость ответа /api/radio/media/audio/{id} на проде. Если среднее время first-byte > 2-3 сек или есть выбросы > 10 сек — гипотеза #1.

  ---
6. Минимальный фикс-набор

Если делать малый диф, который закрывает большинство сценариев:

A. Обработчики событий в JS

setAudioElement: (dotnetHelper) => {
const audio = document.getElementById("audio");
audio.ontimeupdate = () => {
if (audio.currentTime == null || isNaN(audio.currentTime)) return;
if (audio.duration == null || isNaN(audio.duration)) return;
dotnetHelper.invokeMethodAsync("OnAudioProgress", audio.currentTime, audio.duration);
};
audio.onended = () => dotnetHelper.invokeMethodAsync("OnAudioEnded");
audio.onerror = () => dotnetHelper.invokeMethodAsync("OnAudioError", audio.error ? audio.error.code : 0);
audio.onwaiting = () => dotnetHelper.invokeMethodAsync("OnAudioBuffering", true);
audio.onplaying = () => dotnetHelper.invokeMethodAsync("OnAudioBuffering", false);
audio.onstalled = () => dotnetHelper.invokeMethodAsync("OnAudioBuffering", true);
},

B. Защита OnAudioProgress

public void OnAudioProgress(double currentTime, double duration)
{
_lastTimeUpdate = DateTime.UtcNow;
if (duration < 2 || currentTime < 0.5) return;     // кривой duration / старт
var diff = duration - currentTime;
if (diff < StartOverTime)
_ = SetNext();
}

C. Использовать ended вместо эвристики

Полагаться на OnAudioEnded для естественного перехода, а OnAudioProgress использовать только как keepalive для _lastTimeUpdate.

D. Сериализация SetNext

private readonly SemaphoreSlim _setNextLock = new(1, 1);

private async Task SetNext()
{
if (!await _setNextLock.WaitAsync(0)) return;      // отбрасываем дубликаты
try
{
var song = State.IncSongIndex();
...
}
finally { _setNextLock.Release(); }
}

E. Приостановка watchdog при буферизации

private bool _isBuffering;

[JSInvokable]
public void OnAudioBuffering(bool buffering)
{
_isBuffering = buffering;
if (!buffering) _lastTimeUpdate = DateTime.UtcNow;
}

bool Check()
{
if (_audioUrl != url) return false;
if (!_isBuffering && DateTime.UtcNow - _lastTimeUpdate >= updateTimeSpan) return false;
return !State.Token.IsCancellationRequested;
}

F. Полноценный Dispose

private CancellationTokenSource? _loopCts;

protected override void OnInitialized()
{
_loopCts = CancellationTokenSource.CreateLinkedTokenSource(State.Token);
State.Started += OnStartedHandler;
State.SkipRequested += OnSkipHandler;
State.PlaylistChanged += OnPlaylistHandler;
State.VolumeChanged += OnVolumeHandler;
}

public void Dispose()
{
_loopCts?.Cancel();
_loopCts?.Dispose();
State.Started -= OnStartedHandler;
State.SkipRequested -= OnSkipHandler;
State.PlaylistChanged -= OnPlaylistHandler;
State.VolumeChanged -= OnVolumeHandler;
_dotNetRef?.Dispose();
}

  ---
7. Долгосрочная рекомендация

JS-слой плеера слишком тонкий — всю синхронизацию он перекладывает на C# через watchdog и эвристики. У HTML Media API достаточно событий, чтобы знать
точное состояние: loadedmetadata, canplay, playing, waiting, stalled, ended, error, abort. Правильная модель — это state-machine на JS, отправляющая
конкретные transitions в C#, а не «раз в N ms шлю timeupdate, а Шарп сам догадывается». Это убирает все упомянутые риски разом, но это уже рефакторинг,
а не точечный фикс.