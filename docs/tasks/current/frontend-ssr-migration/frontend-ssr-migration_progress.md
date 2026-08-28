## frontend-ssr-migration — Рабочие заметки

### Статус: Не начата

### Заметки
<!-- Находки, решения и полезная информация по ходу реализации -->

Порядок этапов важен: шаг 1 (диагностика) блокирует шаги 2-3, шаг 2 (вынос интеропа в JS) блокирует шаг 3 (переезд на SSR). Шаг 4 независим и может быть сделан в любой момент.

### 00:00 Постановка задачи
Симптом на старом телевизоре разобран по коду. Вечное колесо — это статический блок `.splash` из `Client/wwwroot/index.html`, а не рендер Blazor: `Program.cs` монтирует `Routes` в `#app` и затирает заглушку при первом рендере. Значит, рантайм WASM не стартовал.

Измерен реальный вес клиента из `Client/obj/Release/net10.0`: publish после тримминга 11 MB, brotli 4.8 MB в 122 файлах, gzip 4.7 MB, рантайм `dotnet.native` 956 KB brotli. Тримминг работает корректно (untrimmed `bin/Release` — 45 MB), так что резерв не в нём.

Отклонён вариант «сервер ждёт длительность трека и шлёт следующий»: серверный таймер расходится с реальным воспроизведением из-за буферизации, и расхождение накапливается монотонно. Масштаб виден по `AudioPlayerTiming`: `BufferingTimeout = 90s`. Плюс обрыв SignalR при такой схеме означает тишину до реконнекта.

Принято решение резать не сам интероп, а его частоту: из 18 подписок в `audio-player-events.js` шумит одна (`timeupdate`, ~4 Hz), и нужна она только для отметки «музыка идёт», которую JS знает локально. Целевая поверхность — 2 сообщения на трек вместо ~700.

### 01:00 Решение пользователя: делаем полный переезд на SSR
Шаг 1 (диагностика на телевизоре) пропущен по указанию пользователя. Выполняются шаги 2 и 3 подряд: сначала вынос состояния плеера в JS, затем переезд на Blazor Web App с `InteractiveServer`. Остаточный риск из брифа сохраняется: если ТВ спотыкается об ES6, а не об WASM, переезд сам по себе не поможет — поэтому весь новый JS пишется в ES5-совместимом синтаксисе (шаг 4.3 выполняется попутно).

### 01:10 Разведка кодовой базы
Проверено, что медиа-URL стабильны и не протухают: `MediaStorage.GetAudioUrl(id)` возвращает относительный `/api/radio/media/audio/{id}`, картинки — `/api/radio/media/images/{key}`. Значит очередь треков и пачку картинок можно безопасно отдать в JS заранее, срок жизни ссылок не ограничен.

Следствие для шага 3.5: прокси `app.Map("/api/{**path}")` в `Server/Program.cs` **удалять нельзя** — по этим относительным URL браузер тянет само аудио и картинки. Переносится только серверная часть: `RadioApi` перестаёт ходить через собственный прокси и начинает использовать именованный HttpClient `meta` напрямую. Это отличие от плана в `_info.md`.

`BlazorBlueprint.*` в проекте не используется — только `AddBlazorBlueprintComponents()` в `Client/Program.cs` и `@using` в `_Imports.razor`. Удаляется (частично закрывает шаг 4.4 про тримминг).

Frontend в проде — одна реплика (`backend/Tools/deploy/docker-compose.yaml`), sticky-sessions под circuit не нужны, Traefik проксирует WebSocket по умолчанию.

### 01:20 Принятая архитектура интеропа
JS становится владельцем воспроизведения, .NET — только выбор треков.

.NET -> JS (редкие вызовы): `radioPlayer.init(ref, config)`, `enqueue(tracks)`, `start()`, `skip()`, `reset()`, `setVolume(v)`.
JS -> .NET (2 вызова на трек): `OnTrackStarted(token, songId)` и `OnTrackFinished(token, songId, reason, diagnostics)`, плюс редкий `OnQueueStarved(reason)`.

Watchdog (`startup` / `progress` / `buffering` таймауты), resume-проба и накопление диагностики целиком переезжают в JS. Диагностика не теряется: копится по треку и уходит одним типизированным payload вместе с `OnTrackFinished`.

Prerendering отключается (`InteractiveServerRenderMode(prerender: false)`) — иначе `OnInitializedAsync` отработает дважды и `IJSRuntime` упадёт на префендере. Побочный плюс: splash остаётся видимым ровно до подключения circuit.

### 02:00 Шаг 2 выполнен: состояние плеера вынесено в JS
Написаны четыре ES5-файла в `backend/Frontend/wwwroot/js/`:
- `radio-core.js` — хелперы и fire-and-forget обёртка над интеропом (мёртвый circuit не должен ронять локальное воспроизведение).
- `radio-player.js` — очередь, воспроизведение, watchdog, resume-проба, накопление диагностики, preload следующего трека за 20 секунд до конца текущего.
- `radio-images.js` — слайдшоу целиком в JS, .NET отдаёт пачками по 24 URL.
- `radio-ui.js` — автоскрытие контролов и снятие splash.

Интероп схлопнут до 2 сообщений на трек (`OnTrackStarted`, `OnTrackFinished`) плюс редкий `OnQueueStarved` вместо ~700 на трек. `timeupdate` больше не покидает браузер.

Диагностика не потеряна: `TrackDiagnostics` несёт ct/dur/readyState/networkState, счётчики и суммарное время буферизации, все попытки resume с их исходом, кольцевой буфер из последних 40 медиа-событий и userAgent. Всё уходит одним payload вместе с причиной завершения трека.

Тонкость с resume-бюджетом: `timeupdate` подтверждает только «поток идёт» и бюджет попыток не сбрасывает, а `playing`/`canplay`/`canplaythrough` и возврат из скрытой вкладки — сбрасывают. Это повторяет прежнее поведение `resetResumeAttempts` из `AudioPlayer.Events.cs`.

Барьерный токен вместо generation: при смене плейлиста `_barrierToken` фиксирует последний выданный токен, и завершения треков старого плейлиста не считаются в глубину очереди и не репортятся как скипы.

### 02:30 Шаг 3 выполнен: переезд на InteractiveServer
`App.razor` заменил `index.html`, prerendering выключен (`InteractiveServerRenderMode(prerender: false)`). Splash живёт вне интерактивного острова и снимается из `MainLayout.OnAfterRenderAsync` через `radioUi.ready()`.

Шаг 3.8 (поведение при обрыве circuit) закрыт: `#components-reconnect-modal` переопределён как узкий баннер сверху, дефолтный полноэкранный оверлей не появляется, звук под ним продолжает играть. `Blazor.start` настроен на 100 попыток с интервалом 2 секунды.

Прокси `/api/{**path}` оставлен (иначе браузер не получит аудио и картинки), `RadioApi` переведён на именованный HttpClient `meta` — лишний хоп через собственный прокси убран.

### 03:00 Объединение трёх проектов в один (по запросу пользователя)
После переезда деление Client/Server/Shared потеряло смысл: WASM-границы больше нет. Всё слито в `backend/Frontend/Frontend.csproj` (Sdk.Web, AssemblyName `Frontend`). Namespaces: `Frontend.Client.Components` -> `Frontend.Components`, `Frontend.Client.Services` -> `Frontend.Services`, `Frontend.Shared` сохранён (его использует `Tools/Tests`).

Обновлены все точки, где фигурировали три проекта: `post-radio.slnx`, `Aspire.csproj` + `Aspire/Program.cs` (`Projects.Server` -> `Projects.Frontend`), `Tools/Tests/Tests.csproj`, `Orchestration/Dockerfile` (restore и publish-цикл), `deploy/docker-compose.yaml`, `deploy/docker-compose.local.yaml`, `deploy/COOLIFY.md`, `tools/scripts/publish-local.sh`. Имя ассембли в деплое сменилось `Frontend.Server` -> `Frontend`.

Инцидент: скрипт слияния сделал `rm -rf Client Server` после упавшего `git mv`, снеся `Components/` и новый `wwwroot/js/`. Отслеживаемые файлы восстановлены через `git checkout`, JS — из publish-вывода в scratchpad, остальные новые файлы переписаны заново. Итоговое состояние проверено сборкой и headless-прогоном.

### 03:20 Проверки
- `dotnet build backend/post-radio.slnx` — 0 ошибок.
- `dotnet publish` фронтенда: 1.1 MB против прежних 11 MB тримленного WASM-вывода, `_framework/blazor.web.js` на месте, WASM-полезной нагрузки нет.
- Headless Chrome через CDP: circuit поднимается, splash снимается, `#audio` и кнопка Play отрисованы, 6 слотов картинок на месте, `window.radioPlayer.init` доступен, исключений в консоли нет.
- `PlayableTrackPolicy` тесты (единственные, что зависели от `Frontend.Shared`): 20/20 passed.
