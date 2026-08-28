## Задача: переезд Frontend с Blazor WebAssembly на серверный рендеринг

### Цель
Добиться работоспособности `Post Radio` на старых браузерах smart-TV, где сейчас страница висит на splash-заглушке. Основной путь — перевод `backend/Frontend` с standalone Blazor WebAssembly на Blazor Web App с `InteractiveServer`. Обязательное условие переезда — предварительный вынос состояния плеера в JS, иначе SignalR-трафик станет неприемлемым.

### Контекст

**Симптом.** На старом телевизоре страница грузится, видна тёмная заглушка с крутящимся кольцом и надписью POST RADIO, дальше ничего не происходит.

**Диагноз по коду.** Это колесо не рисует Blazor. Единственный спиннер в проекте — статический, в `backend/Frontend/Client/wwwroot/index.html` (блок `.splash`, чистый CSS без JS). В `backend/Frontend/Client/Program.cs` корневой компонент монтируется как `builder.RootComponents.Add<Routes>("#app")`, то есть содержимое `#app` затирается при первом рендере. Вечное кольцо означает, что первый рендер не состоялся — рантайм WASM не стартовал.

Вариант «рантайм жив, но не пришли данные» исключён: `Home.razor` создаёт `SessionState` синхронно и рендерит дочерние компоненты, не дожидаясь `LoadOptions()`. При живом рантайме был бы виден плеер и кнопка Play.

**Измеренный вес текущего клиента** (`backend/Frontend/Client/obj/Release/net10.0`):

| Метрика | Значение |
|---------|----------|
| publish после тримминга (webcil) | 11 MB |
| publish, brotli | 4.8 MB / 122 файла |
| publish, gzip | 4.7 MB / 65 файлов |
| рантайм `dotnet.native`, brotli | 956 KB |
| ICU (`icudt_CJK` / `icudt_no_CJK`), brotli | 244 KB / 220 KB |

Под `InteractiveServer` первичная загрузка сводится к HTML + `blazor.web.js` + CSS, порядка 50-100 KB. Но решающий выигрыш не в трафике, а в том, что исчезает этап компиляции и инстанцирования 122 wasm-модулей — на слабом CPU телевизора он обычно дороже самой загрузки.

**Почему нужен предварительный рефакторинг интеропа.** `backend/Frontend/Client/wwwroot/js/audio-player-events.js` подписывает 18 аудиособытий, каждое уходит в .NET через `dotnetHelper.invokeMethodAsync("OnAudioEvent", ...)`. Среди них `timeupdate` — примерно 4 Hz. Сейчас это локальные вызовы внутри вкладки и стоят они ноль; под Blazor Server каждый станет сетевым round-trip. Для радио с длинными сессиями это меняет профиль нагрузки принципиально.

При этом `timeupdate` нужен ровно для `MarkProgress(now, resetResumeAttempts: false)` — знания «музыка ещё идёт», которое у JS есть локально и бесплатно.

**Отвергнутая альтернатива.** Рассматривался вариант «сервер ждёт `await Task.Delay(DurationMs)` и шлёт следующий трек». Отклонён: серверный таймер меряет настенное время, а буферизация, паузы и уход ТВ в сон сдвигают реальное воспроизведение назад, и расхождение накапливается монотонно. Масштаб проблемы виден по константам `AudioPlayerTiming`: `BufferingTimeout = 90s`, `NormalProgressTimeout = 30s`. Дополнительно при такой схеме обрыв SignalR означает тишину до реконнекта, тогда как сейчас клиент обрывов не замечает.

### Шаги реализации

**1. Диагностика причины (блокирующий этап, до любых правок архитектуры)**
  1.1. Добавить временный диагностический оверлей: `typeof WebAssembly`, `navigator.userAgent`, перехват `window.onerror`, таймаут "рантайм не стартовал за 20 секунд" — `backend/Frontend/Client/wwwroot/index.html`
  1.2. Открыть страницу на целевом телевизоре, снять вывод
  1.3. Зафиксировать результат в `_progress.md` и выбрать ветку:
       - `WebAssembly === "undefined"` — переезд на SSR обязателен, идти по шагам 2-3
       - `CompileError` при инстанцировании — сначала попробовать шаг 4.1, он на порядок дешевле
       - ошибка синтаксиса в `audio-player-*.js` — переезд не поможет, идти в шаг 4.3

**2. Вынос состояния плеера в JS (полезен независимо от исхода шага 1)**
  2.1. Перенести логику watchdog в JS: пороги из `AudioPlayerTiming` (`BufferingTimeout`, `NormalProgressTimeout`, `StartupTimeout`, `WatchdogInterval`) — `backend/Frontend/Client/wwwroot/js/audio-player-watchdog.js` [новый файл]
  2.2. Вести прогресс и буферизацию локально в JS, убрать `timeupdate`, `waiting`, `stalled`, `suspend`, `canplay`, `seeking` и прочую диагностику из списка событий, уходящих в .NET — `backend/Frontend/Client/wwwroot/js/audio-player-events.js`
  2.3. Свести JS -> .NET к одному вызову на трек `trackFinished(generation, reason, snapshot)`, где `reason` это `ended` / `error` / `buffering-timeout` / `progress-timeout` / `startup-timeout`, а `snapshot` несёт накопленный диагностический payload — `backend/Frontend/Client/wwwroot/js/audio-player-events.js`, `backend/Frontend/Client/Components/AudioPlayerParts/AudioPlayer.Events.cs`
  2.4. Оставить в .NET только выбор трека: упростить `Loop()` до выдачи очереди, убрать `WatchCurrentTrack` — `backend/Frontend/Client/Components/AudioPlayerParts/AudioPlayer.Loop.cs`, `AudioPlayer.PlaybackState.cs`, `AudioPlayerTiming.cs`
  2.5. Передавать очередь из 2-3 URL вместо одного, чтобы JS доигрывал через разрыв связи и делал preload следующего трека — `backend/Frontend/Client/Components/AudioPlayerParts/AudioPlayer.SetNext.cs`, `backend/Frontend/Client/wwwroot/js/audio-player-playback.js`
  2.6. Отдать в JS список изображений и интервал вместо вызова на каждую смену (`ImageSwitchIntervalMs` = 8000, то есть ~450 вызовов в час на зрителя) — `backend/Frontend/Client/Components/ImagesView.razor`, `backend/Frontend/Client/wwwroot/js/image-preloader.js`
  2.7. Убедиться, что после рефакторинга текущий WASM-вариант работает без регрессий, и только затем идти в шаг 3

**3. Переезд на Blazor Web App с InteractiveServer**
  3.1. Сменить SDK клиента с `Microsoft.NET.Sdk.BlazorWebAssembly` на Razor-библиотеку компонентов — `backend/Frontend/Client/Client.csproj`
  3.2. Превратить `index.html` в `App.razor` с `HeadOutlet` и `<HeadContent>`, перенести `.splash` в fallback до гидрации — `backend/Frontend/Client/wwwroot/index.html` [удаляется], `backend/Frontend/Server/Components/App.razor` [новый файл]
  3.3. Зарегистрировать `AddRazorComponents().AddInteractiveServerComponents()` и `MapRazorComponents<App>().AddInteractiveServerRenderMode()` — `backend/Frontend/Server/Program.cs`
  3.4. Задать render mode на корневом компоненте и снять зависимость от `WebAssemblyHostBuilder` — `backend/Frontend/Client/Components/Routes.razor`, `backend/Frontend/Client/Program.cs` [удаляется]
  3.5. Перевести `RadioApi` на прямой вызов meta-сервиса вместо `BaseAddress = HostEnvironment.BaseAddress`, убрать прокси `app.Map("/api/{**path}", ...)` — `backend/Frontend/Client/Services/RadioApi.cs`, `backend/Frontend/Server/Program.cs`
  3.6. Проверить время жизни `SessionState` в рамках circuit: сейчас он создаётся в `OnInitializedAsync` и освобождается в `Dispose`, под Server это привязано к circuit, а не к вкладке — `backend/Frontend/Client/Components/Pages/Home.razor`, `backend/Frontend/Client/Services/SessionState.cs`
  3.7. Перенести JS-файлы в хост и настроить порядок подключения — `backend/Frontend/Server/wwwroot/js/`
  3.8. Настроить поведение при обрыве circuit: телевизор в сне или с моргающим Wi-Fi будет ловить reconnect регулярно, дефолтный оверлей "Attempting to reconnect" поверх плеера неприемлем

**4. Дешёвые улучшения, не требующие переезда**
  4.1. Снизить требования к WASM до MVP: `<WasmEnableSIMD>false</WasmEnableSIMD>`, `<WasmEnableExceptionHandling>false</WasmEnableExceptionHandling>` — `backend/Frontend/Client/Client.csproj`
  4.2. Проверить `<InvariantGlobalization>true</InvariantGlobalization>`: форматирование в `AudioPlayerFormatters` идёт через `CultureInfo.InvariantCulture`, ICU может оказаться не нужен — минус ~220 KB brotli и один запрос
  4.3. Транспилировать `audio-player-*.js` и `image-preloader.js` до ES5: сейчас там стрелочные функции, `const`, деструктуризация, `for...of` — старый WebKit это не распарсит независимо от рантайма
  4.4. Проверить, что переживает тримминг: в untrimmed-сборке видны `System.Private.Xml` (3.0 MB), `AngleSharp` + `AngleSharp.Css` (864 KB), `System.Data.Common` (988 KB), `System.Private.DataContractSerialization` (832 KB) — вероятный источник `BlazorBlueprint.Components`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Frontend/Client/wwwroot/index.html` | Splash-заглушка, точка диагностики, исчезает при переезде |
| `backend/Frontend/Client/Program.cs` | `RootComponents.Add<Routes>("#app")`, WASM host builder |
| `backend/Frontend/Client/Client.csproj` | SDK проекта, флаги WASM и глобализации |
| `backend/Frontend/Server/Program.cs` | Хостинг, прокси `/api/{**path}`, `MapFallbackToFile` |
| `backend/Frontend/Client/wwwroot/js/audio-player-events.js` | 18 подписок и весь шум интеропа, главный объект рефакторинга |
| `backend/Frontend/Client/Components/AudioPlayerParts/AudioPlayerTiming.cs` | Пороги watchdog, переезжают в JS |
| `backend/Frontend/Client/Components/AudioPlayerParts/AudioPlayer.Loop.cs` | Цикл воспроизведения и `WatchCurrentTrack` |
| `backend/Frontend/Client/Components/AudioPlayerParts/AudioPlayer.Events.cs` | `OnAudioEvent`, продвижение по `ended` |
| `backend/Frontend/Client/Components/AudioPlayerParts/AudioPlayer.Resume.cs` | Resume-проба перед скипом, остаётся в JS |
| `backend/Frontend/Client/Components/ImagesView.razor` | Смена изображений, вызов `imagePreloader.load` |
| `backend/Frontend/Client/Services/SessionState.cs` | Shuffle, `_songIndex`, presence-loop, время жизни под circuit |
| `backend/Frontend/Client/Services/RadioApi.cs` | HTTP-клиент, `BaseAddress` завязан на WASM host |
| `backend/Frontend/Shared/PlaylistDto.cs` | `SongDto.DurationMs` (nullable), `PlayableTrackPolicy` |

### Документация к прочтению
- `.claude/docs/BLAZOR.md` — правила Razor UI, `[Inject]` в `@code`, `UiComponent`, ранний return.
- `.claude/docs/CODE_STYLE_FULL.md` — стиль C# и порядок членов.
- `.claude/docs/COMMON_LIFETIMES.md` — время жизни подписок при переходе на circuit-scoped состояние.
- `.claude/docs/DEPLOY.md` — dev и prod не делят runtime, правки в Aspire не влияют на Compose.

### Риски
- **Шаг 3 может не решить проблему.** Если телевизор спотыкается не о WASM, а об ES6-синтаксис в JS, SSR даст быстро загружающийся неработающий плеер. Поэтому шаг 1 блокирующий.
- **Отзывчивость меняет профиль.** Под `InteractiveServer` каждое действие — сетевой round-trip. Для Play/Skip это незаметно, но только при выполненном шаге 2; без него `timeupdate` на 4 Hz через сеть неприемлем.
- **Стоимость сессии на сервере.** Каждый слушатель держит circuit с состоянием. Для радио с длинными сессиями нужно оценить память до выката.
- **Обрывы circuit.** ТВ уходит в сон, Wi-Fi моргает. Очередь треков из шага 2.5 частично компенсирует, но поведение UI при reconnect надо продумать явно (шаг 3.8).
- **`SongDto.DurationMs` nullable и берётся из тегов.** Полагаться на него как на источник истины о таймингах нельзя, у VBR MP3 метаданные врут регулярно.
- **Потеря диагностики.** Сейчас в .NET стекается подробная телеметрия по скипам (`ReportResumeAttempt`, `AudioStateSnapshot`). При схлопывании интеропа её надо сохранить, отправляя пакетом вместе с `trackFinished`, а не потерять.
