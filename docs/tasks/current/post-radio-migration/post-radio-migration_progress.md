# Post Radio Migration — Рабочие заметки

## Статус: В работе (разделение сессии)

## Выполнено

- [x] Копирование mines-leader → post-radio без bin/client/obj/publish/shared/art/docs/tools
- [x] Агрессивное удаление мина‑контента (Game/GameGateway/Meta.Bots/Meta.Matches/Meta.Users/Cluster.Configs мина/Common.Network/MetaGateway.Matchmaking/MetaGateway.UserFlow/MetaGateway.IdentityEndpoints)
- [x] slnx → post-radio.slnx, убраны ссылки на shared.csproj во всех csproj
- [x] Восстановлены: Tools/DeploySetup + Tools/Generators (реальный source generator), Cluster/Configs инфра‑конфиги
- [x] Common/Storage (IObjectStorage+ObjectStorage+MinioCredentials+AddObjectStorage) + Minio 6.0.3
- [x] SoundCloudExplode 1.6.7 + AudioServicesStartup.InitializeAsync
- [x] Meta/Audio под StateCollection паттерн: Playlist/Song grains c [State]/`State<T>`/[GrainState], IPlaylistsCollection:IStateCollection<Guid,PlaylistState>, ISongsCollection:IStateCollection<long,SongState>+ByPlaylist
- [x] Meta/Audio/PlaylistFactory (creates grain), PlaylistLoader (SoundCloud tracks → MinIO upload, adapted to IAsyncEnumerable<Track>)
- [x] Meta/Images/ImagesCollection + EnsureBucket на старте
- [x] Meta/Online (OnlineTracker + OnlineListener) — зарегистрирован в DI, UI не использует
- [x] Frontend split: Shared (DTO), Client (WASM — razor), Server (статический хост). Csproj/папки переименованы без префикса Frontend.
- [x] MetaGateway/RadioEndpoints (/api/radio/playlists|songs|songs/{id}/stream|images|images/{index})
- [x] Frontend Client полная вёрстка: SessionState + AudioPlayer (JS interop, HTML5 audio loop) + ImagesView (crossfade 2 слоя) + SongDataView + ControlsView (radio playlists + vertical volume + skip) + StartWait + MainLayout + Home
- [x] Frontend/Client/Services/RadioApi (IRadioApi с HttpClient)
- [x] Console/Home переработан: добавлена Radio секция (Playlists/Songs/Images/Migrations nav cards)
- [x] Console/Radio: Playlists.razor (grid + inline create через IPlaylistFactory), Songs.razor (3 сортировки), Images.razor (count+refresh), Migrations.razor (run MetaDataMigration), PlaylistDetail.razor (+ кнопка Load songs с progress логом через IPlaylistLoader)
- [x] Console/Common/PageHeader.razor переписан на BlazorBlueprint
- [x] ConsoleConstants.Pages — добавлены Playlists/PlaylistDetail/Songs/Images/Migrations
- [x] Удалены: ServiceTag.Game + GameServerOverview + GAME секция на Home + Matchmaking/SnapshotDiffGuard tiles в FeaturesStatusWidget
- [x] Aspire/Program.cs: MinIO container (minio/minio, порт 9000/9001, env Minio__Endpoint/AccessKey/SecretKey на все проекты), WaitFor(minio) для Silo
- [x] ProjectsSetupExtensions.AddBase: .AddConfigs().AddObjectStorage().AddAudioServices().AddImagesServices().AddOnlineServices()
- [x] MinIO UseSSL optional (по умолчанию false для local dev), http→https replacement убран из GetUrl

### Сессия #3 — разделение Console на Main и Infrastructure

- [x] `ConsoleConstants.Pages.Infrastructure = "/infrastructure"` добавлен
- [x] Новая страница `Console/Home/Infrastructure.razor` (`/infrastructure`) — `HomeInfrastructureSection` + `FeaturesStatusWidget`
- [x] `Console/Home/Home.razor` переписан как Main: плитки Radio (Playlists/Songs/Images/Migrations), справа — список текущих плейлистов (IPlaylistsCollection), ниже — список всех песен с live search (ISongsCollection)
- [x] `Orchestration/ConsoleGateway/Shared/MainLayout.razor` — топбар очищен, оставлены только `Main` (`/`) и `Infrastructure` (`/infrastructure`)

### Сессия #2 — подключение Frontend к Aspire + BFF proxy + UI

- [x] **Aspire AddProject для Frontend** — Frontend/Server не был зарегистрирован в AppHost. Добавлено:
  - `backend/Orchestration/Aspire/Aspire.csproj` — `<ProjectReference Include="..\..\Frontend\Server\Server.csproj"/>`
  - `backend/Orchestration/Aspire/Program.cs` — `using Frontend = Projects.Server;` + `var frontend = builder.AddProject<Frontend>("frontend").WithReference(meta);` + `frontend.WaitFor(meta);`
- [x] **Port/launchSettings фиксы** — изначально был конфликт dcp proxy(5200) ↔ Kestrel(5200). Итог: `launchSettings.json` с `applicationUrl: http://localhost:5200`, `launchBrowser: false`, `inspectUri` для Blazor WASM debugger, `ASPNETCORE_ENVIRONMENT=Development`. `WithHttpEndpoint(5200,...)` в Aspire убрано (дубликат endpoint'а).
- [x] **BFF proxy /api/*** — Frontend.Server теперь форвардит `/api/{**path}` на MetaGateway через `IHttpClientFactory`. URL meta читается из `builder.Configuration["services:meta:http:0"]` (прокидывается Aspire `WithReference(meta)`).
- [x] **Request logging middleware** — `app.Use` перед pipeline логирует `[Req] METHOD PATH QUERY`.
- [x] **HTTPS redirection** убран из dev (только non-dev) — когда был активен без https профиля, давал бесконечный редирект.
- [x] **RadioApi leading slash** — все URL в `Frontend/Client/Services/RadioApi.cs` переведены на абсолютный путь (`"/api/radio/..."`). Относительные без слеша резолвились относительно текущего роута → падали в MapFallbackToFile → HTML → `JsonException: ExpectedStartOfValueNotFound, <`.
- [x] **RadioApi try/catch + ILogger** — все методы обёрнуты в try/catch, возвращают безопасные дефолты (пустой список / 0 / пустая строка), warning в лог. Больше не падают UI компоненты при пустых коллекциях.
- [x] **index.html фиксы**:
  - Добавлен CSS для `#blazor-error-ui { display: none; ... }` — раньше плашка "An unhandled error has occurred" показывалась постоянно.
  - Красивый splash-screen: тёмный radial gradient + CSS-спиннер `.ring` + пульсирующая `.title` POST RADIO. Был голый "Loading...".
- [x] **Rider browser path** — на Arch Linux нет `google-chrome`, только `/usr/bin/google-chrome-stable`. Исправлено пользователем в Rider Settings → Tools → Web Browsers.

## Текущий момент остановки

**Последний рабочий статус**: Всё запускается в Aspire (Run и Debug), WASM грузится, API работают через BFF-прокси, 401 из SoundCloud не проверен в этой сессии (переключились на связку Frontend↔Aspire).

**Что явно работает**:
- curl `http://localhost:5200/api/radio/images` → `{"count":0}` ✓
- curl `http://localhost:5200/api/radio/playlists` → `[]` ✓
- curl `http://localhost:7101/api/radio/*` (MetaGateway напрямую) — тот же JSON ✓
- WASM грузится в инкогнито — ошибок в консоли нет

**Что осталось открытым**:
1. **Пользователь не проверил SoundCloud loading** после апгрейда SoundCloudExplode 1.6.7 (из сессии #1). Нужно нажать кнопку "Load songs" на `PlaylistDetail.razor` и убедиться что треки качаются.
2. **Rider Blazor WASM debugger** — работает после указания корректного пути к Chrome. Если снова «browser.empty» — проверить `Settings → Tools → Web Browsers`.
3. **Browser cache pitfall**: при повторной загрузке страницы WASM агрессивно кеширует DLL — при изменениях в Client.csproj лучше тестить в инкогнито или `Clear storage`.

## Важные находки

### Сессия #1 — API различия mines-leader vs post-radio-old
- **StateCollection** (mines-leader) vs **ViewableDictionary+MessageQueueId** (post-radio-old) — полностью разные паттерны. StateCollection делает ReadAll через IStateStorage + ListenDurableQueue автоматически. Advise идёт через `.Updated` (IViewableDelegate), не напрямую на коллекции.
- **SongState** не имеет `.Id`, нужно итерировать `foreach (var (id, state) in Collection)` и собирать `SongData` вручную.
- **IOrleans** заменяет `IClusterClient`. `_orleans.GetGrain<T>(id)` сигнатура совпадает.
- **MessageQueueId** → **IDurableQueueId** / `DurableQueueId(string id)`. `ListenQueue` → `ListenDurableQueue`. `PushDirectQueue` та же сигнатура.
- **SoundCloudExplode API изменился**: `GetTracksAsync` теперь `IAsyncEnumerable<Track>` (не `Task<List<Track>>`). `InitializeAsync()` исчез в 1.5.0, вернулся в 1.6.x.
- **SoundCloudExplode.Common namespace** больше не существует в 1.6.7 — использовать только `SoundCloudExplode` и `SoundCloudExplode.Exceptions`.
- Track type находится в `SoundCloudExplode.Tracks.Track`.

### Razor / BlazorBlueprint
- `@using global::Common` обязательно в Console razor, т.к. `Console.Common` shadow'ит `Common` namespace.
- `IOperationProgress` живёт в `Common.Extensions`, НЕ в `Common`.
- Razor в Console.csproj использует оба — BlazorBlueprint (основной) + MudBlazor (fallback для razor pages которые я пока не переписал, но сейчас все наши Radio razor на BB). Оставил MudBlazor в Console.csproj — может пригодиться.
- `DialogOptions` и `InputType` — неоднозначны между BB и MudBlazor, нужен fully-qualified type (`MudBlazor.DialogOptions`, `BlazorBlueprint.Components.InputType`).
- В Frontend/Shared.csproj убрана Razor SDK — теперь plain `Microsoft.NET.Sdk` для DTO.

### CPM (Central Package Management)
- `Microsoft.AspNetCore.App.Internal.Assets` неявно включается Web SDK и BlazorWebAssembly SDK — если указать в `Directory.Packages.props`, WASM/Server ругаются NU1009. Решение: **явный `PackageReference` в Client.csproj и Server.csproj** (тогда NU1009 не срабатывает), версия в props.

### Generator
- `StatesLookupGenerator` сканирует все `[GrainState]` атрибуты и создаёт partial `StatesLookup` класс в `namespace Common` + статический `GeneratedStatesRegistration.AddAllStates(List<GrainStateInfo>)`.
- Без него `ITaskBalancerConfig` не регистрируется → DI ValidateOnBuild ошибка.

### MinIO
- `.WithSSL()` в `MinioClient` forces HTTPS. Для localhost MinIO в Aspire не подходит → `UseSsl` field в `MinioCredentials` (default false).
- `GetAllKeys` обёрнут try/catch чтобы при отсутствии bucket не валить startup. Bucket-ы создаёт `ImagesCollection.OnCoordinatorSetupCompleted` через `EnsureBucket("images")` + `EnsureBucket("audio")`.

### Aspire
- Сервисы `Finished` в UI Aspire даже если упали с exit code != 0 — это нормальное поведение. Статус смотреть в logs/details.
- `ClusterParticipantStartup` ждёт всех сервисов из `DeployConstants.RequiredServices` — если там остался лишний тег (например `Game`), бесконечно `missing: X`.

### Сессия #2 — Aspire + Blazor WASM hosted pitfalls

- **launchSettings.json + Aspire.WithHttpEndpoint конфликт**: оба создают endpoint с именем "http" → `DistributedApplicationException: Endpoint with name 'http' already exists`. Выбирать ОДИН источник. Мы оставили launchSettings (нужен для Rider WASM debug attach).
- **dcp proxy vs Kestrel port collision**: если запускать Frontend.Server напрямую (не через Aspire) при живом dcp на 5200 — `AddressInUseException`. dcp bind'ится сразу как Aspire стартанёт.
- **Rider Blazor WASM debugger killer**: если в Rider не настроен валидный default browser (с исполняемым файлом), Aspire-плагин стреляет ошибку `!blazor.wasm.debug.exception.browser.empty!` и **убивает дочерний Frontend.Server процесс** ДО старта Kestrel. Лог дочернего процесса в `/tmp/aspire-dcp*/frontend-*_out_*` обрывается после `[?1h=` и `Now listening...` не появляется. Починка — указать валидный путь к браузеру (`/usr/bin/google-chrome-stable` на Arch).
- **WASM cache storage** — `Ctrl+Shift+R` не очищает Blazor WASM кеш DLL. Для тестов после изменений — инкогнито-окно или DevTools → Application → Clear storage.
- **Relative URL pitfall в RadioApi**: `HttpClient.GetFromJsonAsync("api/radio/images")` (без ведущего слеша) при нахождении пользователя на под-роуте (`/player`) резолвится как `/player/api/radio/images`, не попадает в `/api/*` proxy, отдаётся `index.html` через `MapFallbackToFile` → `JsonException` на `<`. **Всегда ставить ведущий слеш.**
- **index.html blazor-error-ui**: стандартный template содержит CSS `#blazor-error-ui { display: none; }` + правила позиционирования. Без него плашка "An unhandled error has occurred" показывается постоянно. В нашем исходном index.html этого CSS не было.
- **BFF proxy паттерн в Frontend.Server**: `IHttpClientFactory("meta")` + `client.BaseAddress = builder.Configuration["services:meta:http:0"]`. Aspire прокидывает эту переменную через `frontend.WithReference(meta)`.

## Измененные файлы (на момент разделения)

Всё в `/projects/post-radio/` (не git repo).

| Категория | Статус |
|-----------|--------|
| Backend domain (Meta/Audio, Meta/Images, Meta/Online) | создано с нуля по паттерну mines-leader |
| Common/Storage | новый модуль (IObjectStorage, MinioCredentials) |
| Cluster/Configs | восстановлены инфра‑конфиги, мина-удалены |
| Console/Home | обновлён, Radio nav карточки |
| Console/Radio | новый модуль (5 pages) |
| Console/Common/PageHeader.razor | переписан на BB |
| Frontend/Shared | создано с нуля (DTO) |
| Frontend/Client | WASM — razor компоненты, RadioApi c абсолютными URL + try/catch + ILogger, index.html с splash + #blazor-error-ui CSS |
| Frontend/Server | thin host + BFF /api/* proxy + request logging + launchSettings.json (inspectUri Blazor WASM debug) |
| Orchestration/MetaGateway/RadioEndpoints.cs | новый |
| Orchestration/Extensions/ProjectsSetupExtensions.cs | очищен от мина, добавлен AddConfigs + AddObjectStorage + AddAudioServices + AddImagesServices + AddOnlineServices |
| Orchestration/Aspire/Aspire.csproj | +ProjectReference Frontend/Server |
| Orchestration/Aspire/Program.cs | MinIO container, Frontend registration (`AddProject<Frontend>("frontend").WithReference(meta)` + `WaitFor(meta)`) |
| Orchestration/MetaGateway/Program.cs | AddRadioEndpoints |
| Cluster/Discovery/ServiceTag.cs | удалён Game |
| Cluster/Discovery/ServiceDiscovery.cs | упрощён, удалён GameServerOverview |
| Cluster/Deploy/DeployConstants.cs | RequiredServices без Game |
| Directory.Packages.props | добавлены Minio + SoundCloudExplode + WebAssembly.* |
| post-radio.slnx | 16 проектов |

## Заметки из сессии

### Решения приняты (сессия #1):
1. **WASM для Frontend** (не InteractiveAuto) — user выбрал после обсуждения: важнее отзывчивость/стабильность, первая загрузка не критична
2. **OnlineTracker отложен** — позже middleware с user activity last-seen (5 мин интервал)
3. **MudBlazor + BlazorBlueprint одновременно** в Console — BB основной, MudBlazor fallback для razor pages где BB компонентов нет (Dialog, Stack layouts)
4. **Namespace flat**: `Meta.Audio` (не `Meta.Audio.Playlists`/`Meta.Audio.Songs`) — унифицировано линтером
5. **Frontend проекты без префикса**: `Client/Server/Shared` в slnx, AssemblyName остался `Frontend.Client/Server/Shared` во избежание конфликтов namespace
6. **SoundCloud кредитация** не нужна — `InitializeAsync` получает публичный client_id через scraping страницы

### Решения приняты (сессия #2):
7. **BFF паттерн для Frontend.Server**: форвардить `/api/*` в MetaGateway через `IHttpClientFactory`, НЕ прямой call из WASM на MetaGateway. Причина: WASM должен общаться только со своим origin, иначе CORS/service discovery complications.
8. **Aspire WithReference(meta)** вместо хардкода URL — Aspire сам прокинет env `services__meta__http__0`.
9. **isProxied: false убран** — оставлен стандартный dcp-прокси от Aspire, launchSettings управляет портом. Это совместимо с Rider WASM debug attach.
