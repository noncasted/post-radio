## frontend-ssr-migration — Результат

### Статус: Задеплоено и проверено в проде, кроме целевого телевизора

### Что сделано

1. **Состояние плеера вынесено в JS (шаг 2).** Очередь, воспроизведение, watchdog, resume-проба и накопление диагностики живут в `wwwroot/js/radio-player.js`. Интероп схлопнут с ~700 сообщений на трек до 2 (`OnTrackStarted`, `OnTrackFinished`) плюс редкий `OnQueueStarved`. `timeupdate` браузер больше не покидает.
2. **Очередь из 3 треков и preload.** JS держит 2-3 готовых URL и доигрывает через обрыв SignalR; следующий трек начинает грузиться за 20 секунд до конца текущего, чтобы вторая загрузка не отбирала полосу у играющей.
3. **Слайдшоу целиком в JS.** `radio-images.js` получает пачками по 24 URL и интервал вместо вызова на каждую смену картинки (~450 вызовов в час на зрителя).
4. **Переезд на Blazor Web App с `InteractiveServer` (шаг 3).** `index.html` заменён на `App.razor`, WASM-рантайм убран полностью.
5. **Поведение при обрыве circuit (шаг 3.8).** `#components-reconnect-modal` переопределён как узкий баннер сверху вместо полноэкранного оверлея; звук под ним продолжает играть. `Blazor.start` — 100 попыток с интервалом 2 секунды.
6. **Prerendering включён.** Краулер получает готовую разметку. Весь интероп сидит в `OnAfterRenderAsync(firstRender)`, который при пререндере не вызывается, поэтому JS стартует ровно один раз — после подъёма circuit.
7. **Три проекта слиты в один.** `Client` + `Server` + `Shared` -> `backend/Frontend/Frontend.csproj`, ассембли `Frontend`.
8. **Весь новый JS написан на ES5** (шаг 4.3): никаких стрелочных функций, `const`/`let`, деструктуризации и `for...of`.

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `backend/Frontend/Frontend.csproj` | Новый объединённый проект (Sdk.Web, AssemblyName `Frontend`) вместо трёх |
| `backend/Frontend/Program.cs` | `AddRazorComponents().AddInteractiveServerComponents()`, `MapRazorComponents<App>()`, `RadioApi` на прямой HttpClient `meta`; прокси `/api/{**path}` сохранён |
| `backend/Frontend/Components/App.razor` | Новый host-шаблон вместо `index.html`: splash, reconnect-баннер, порядок скриптов, `prerender: true` |
| `backend/Frontend/wwwroot/js/radio-core.js` | Хелперы и fire-and-forget обёртка интеропа |
| `backend/Frontend/wwwroot/js/radio-player.js` | Очередь, воспроизведение, watchdog, resume, диагностика, preload |
| `backend/Frontend/wwwroot/js/radio-images.js` | Слайдшоу с preload и кросс-фейдом |
| `backend/Frontend/wwwroot/js/radio-ui.js` | Автоскрытие контролов, снятие splash |
| `backend/Frontend/Components/AudioPlayerParts/AudioPlayer.razor.cs` | Компонент сведён к выбору трека и подписке на события |
| `backend/Frontend/Components/AudioPlayerParts/AudioPlayer.Queue.cs` | Пополнение очереди JS-плеера (заменил `Loop.cs` + `SetNext.cs`) |
| `backend/Frontend/Components/AudioPlayerParts/AudioPlayer.Interop.cs` | Три `[JSInvokable]` колбэка (заменил `Events.cs`) |
| `backend/Frontend/Components/AudioPlayerParts/AudioPlayer.Types.cs` | `QueuedTrack`, `TrackDiagnostics`, `ResumeAttemptInfo`, `AudioEventInfo` |
| `backend/Frontend/Components/AudioPlayerParts/AudioPlayerConfig.cs` | Пороги watchdog как конфиг для JS (заменил `AudioPlayerTiming.cs`) |
| `backend/Frontend/Components/AudioPlayerParts/AudioSkipDetailBuilder.cs` | Детали скипа строятся из `TrackDiagnostics` |
| `backend/Frontend/Components/ImagesView.razor` | Статичная разметка, пачки URL, `OnImagesNeeded` |
| `backend/Frontend/Components/ControlsView.razor` | Загрузка песен плейлиста отложена до интерактивного прохода |
| `backend/Frontend/Components/Layout/MainLayout.razor` | Снятие splash после гидрации |
| `backend/post-radio.slnx`, `Orchestration/Aspire/*`, `Tools/Tests/Tests.csproj`, `Orchestration/Dockerfile`, `deploy/docker-compose*.yaml`, `deploy/COOLIFY.md`, `tools/scripts/publish-local.sh` | Переход на один проект и ассембли `Frontend` |

Удалены: `Client/Program.cs`, `Client/wwwroot/index.html`, `audio-player-*.js`, `image-preloader.js`, `AudioPlayer.Loop.cs`, `AudioPlayer.Events.cs`, `AudioPlayer.SetNext.cs`, `AudioPlayer.PlaybackState.cs`, `AudioPlayer.Resume.cs`, `AudioPlayerTiming.cs`, зависимость `BlazorBlueprint.*` (не использовалась).

### Отличия от плана

- **Шаг 1 (диагностика на телевизоре) пропущен** по указанию пользователя. Соответственно не подтверждено, что причина зависания — именно WASM.
- **Прокси `/api/{**path}` не удалён** вопреки шагу 3.5: по относительным URL вида `/api/radio/media/audio/{id}` браузер тянет само аудио и картинки. Прямой вызов meta переведён только на серверную часть (`RadioApi`).
- **Шаг 2.5 в части «доигрывать через разрыв связи»** реализован через очередь в JS, но при обрыве circuit пополнение очереди останавливается: запас — 2-3 трека, дальше тишина до реконнекта.
- **Prerendering включён**, хотя в плане его не было — потребовалось для индексации.
- **Три проекта слиты в один** — этого в плане не было, запрошено по ходу работы.
- Шаги 4.1, 4.2, 4.4 (флаги WASM, `InvariantGlobalization`, ревизия тримминга) отпали вместе с WASM-сборкой.

### Проверено

- `dotnet build backend/post-radio.slnx` — 0 ошибок.
- `dotnet publish` фронтенда: 1.1 MB против прежних 11 MB тримленного WASM-вывода; `_framework/blazor.web.js` на месте, WASM-полезной нагрузки нет. Первичный HTML — 7.3 KB.
- Headless Chrome через CDP на standalone-публикации: circuit поднимается, splash снимается, `#audio` и кнопка Play отрисованы, 6 слотов картинок на месте, `window.radioPlayer.init` доступен, исключений в консоли нет.
- `node --check` на всех четырёх JS-файлах.
- Тесты `PlayableTrackPolicy` (единственные, зависевшие от `Frontend.Shared`): 20/20 passed.

### Проверено в проде (post-radio.ru)

Headless Chrome через CDP на задеплоенной версии: 4 плейлиста отрисованы, после нажатия Play аудио реально играет (`paused=false`, `currentTime=19.5s`, `readyState=4`), очередь JS держит 3 трека вперёд, название трека доехало до UI через `OnTrackStarted`, картинки крутятся с реальных URL, исключений в консоли нет. Статика: `blazor.web.js` 200 (200 KB), все четыре `radio-*.js` 200, `robots.txt` и `sitemap.xml` 200, `_blazor/negotiate` 200, API отдаёт 448 картинок.

Регрессия с пропажей `wwwroot/_framework/blazor.web.js`, из-за которой SDK был закреплён на `10.0.201`, на `10.0.400` не воспроизвелась.

Не проверены на живых данных: срабатывание watchdog, resume-проба и доставка диагностики — для них нужен сломанный поток.

### Нерешенные вопросы

- **Не проверено на целевом телевизоре.** Если он спотыкается не о WASM, а об ES6 в `blazor.web.js`, переезд не поможет: сам бутстрап Blazor написан на современном JS и переписать его нельзя.
- **Стоимость сессии на сервере не измерена.** Каждый слушатель держит circuit; для радио с длинными сессиями это надо оценить до выката.
- **Sticky sessions.** Сейчас frontend в одну реплику, вопрос не стоит. При масштабировании понадобятся либо липкие сессии на Traefik, либо Azure SignalR-подобная прослойка.
