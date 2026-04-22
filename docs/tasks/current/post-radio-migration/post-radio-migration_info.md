# Post Radio Migration

## Цель

Перенести функционал из `/projects/post-radio-old/` в новый проект `/projects/post-radio/`, используя архитектуру mines-leader как основу. Убрать мина‑специфичный код, сохранить инфраструктуру, портировать domain (Audio/Images/Online), UI (Console), создать Frontend на WASM.

## Исходные условия

- **Источник архитектуры**: `/projects/mines-leader/backend/`
- **Источник фичей**: `/projects/post-radio-old/`
- **Target**: `/projects/post-radio/`
- Копировать всё кроме `bin/client/client_clone_0/obj/publish/shared/art/docs/tools`
- Namespace `PostRadio` (фактически остался generic: Cluster/Common/Meta/Console/Orchestration/Frontend)
- Backend: агрессивно удалить мина‑контент из Meta и Cluster
- Console: переносить в нашу консоль на BlazorBlueprint (+ временно MudBlazor где не было BB‑эквивалента)
- Frontend: **WebAssembly (WASM)** разделение Server/Client/Shared
- Online tracker отложен (заменим user activity poll раз в 5 минут)

## Архитектура решения

### Backend (Orleans + Aspire)
- `Cluster/` — Coordination/Deploy/Discovery/Monitoring/State/**Configs** (только инфра: TaskBalancer/DurableQueue/SideEffects/RuntimePipe/RuntimeChannel/Transaction)
- `Common/` — Reactive/Lifetime/Extensions/**Storage** (IObjectStorage + MinioCredentials)
- `Infrastructure/` — Messaging/Orleans/Data/Execution/Loop/Startup/State
- `Meta/` — **Audio** (Playlists/Songs grains+collections+factory+loader), **Images** (collection), **Online** (tracker — отложен)
- `Console/` — BlazorBlueprint admin UI + Radio pages
- `Orchestration/` — Aspire/ConsoleGateway/Coordinator/Extensions/MetaGateway/Silo
- `Tools/` — DeploySetup (MinIO ensure), Generators (StatesLookup source generator)
- `Frontend/` — WASM player: Shared/Client/Server
- `solution`: `post-radio.slnx`, 16 проектов

### Данные и storage
- PostgreSQL (Aspire) — grain state (audio/images таблицы через `[GrainState]`)
- MinIO (Aspire container) — bucket `audio` (mp3), bucket `images`
- SoundCloud API — скачивание треков через `SoundCloudExplode 1.6.7`

### Frontend (WASM)
- `Frontend/Shared/` — DTO (PlaylistDto/SongDto/ImagesCountDto)
- `Frontend/Client/` — WASM SPA, все razor, `RadioApi` (HTTP клиент), `SessionState`, компоненты: AudioPlayer/ImagesView/SongDataView/ControlsView/StartWait + MainLayout
- `Frontend/Server/` — тонкий ASP.NET хост, отдаёт WASM bundle
- API гейтвей: `MetaGateway` на `/api/radio/*`

## Ключевые файлы

### Регистрация/конфиг
- `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs` — Setup{Silo,MetaGateway,Console,Coordinator}, AddBase цепочка
- `backend/Orchestration/Aspire/Program.cs` — MinIO container + 4 проекта
- `backend/Directory.Packages.props` — версии пакетов
- `backend/post-radio.slnx` — solution

### Backend domain
- `backend/Meta/Audio/Playlists/Playlist.cs` — IPlaylist/PlaylistGrain/PlaylistState (`[GrainState(Table="audio", State="playlist", Lookup="Playlist", Key=Guid)]`)/PlaylistData
- `backend/Meta/Audio/Songs/Song.cs` — ISong/SongGrain/SongState/SongData
- `backend/Meta/Audio/Playlists/PlaylistLoader.cs` — SoundCloud → MinIO upload
- `backend/Meta/Audio/AudioServicesExtensions.cs` / `AudioServicesStartup.cs`
- `backend/Meta/Images/ImagesCollection.cs` — + bucket ensure
- `backend/Common/Storage/ObjectStorage.cs` — MinIO client
- `backend/Cluster/Configs/{ConfigsExtensions,InfrastructureConfigsState}.cs` — инфра‑конфиги
- `backend/Cluster/Discovery/ServiceTag.cs` — enum без Game

### UI
- `backend/Console/Home/Home.razor` — Radio секция
- `backend/Console/Radio/{Playlists,PlaylistDetail,Songs,Images,Migrations}.razor`
- `backend/Console/Radio/MetaDataMigration.cs`
- `backend/Frontend/Client/Components/` — 5 razor + Layout + Home
- `backend/Frontend/Client/Services/{RadioApi,SessionState}.cs`
- `backend/Frontend/Shared/PlaylistDto.cs`
- `backend/Orchestration/MetaGateway/RadioEndpoints.cs` — `/api/radio/*`

### Генератор
- `backend/Tools/Generators/StatesLookupGenerator.cs` — создаёт `StatesLookup` + `GeneratedStatesRegistration.AddAllStates`

## Шаги миграции

- [x] 1. Скопировать mines-leader → post-radio с исключениями
- [x] 2. Удалить мина‑контент (Game, Meta/{Bots,Matches,Users}, GameGateway, мина razor)
- [x] 3. Настроить slnx, убрать shared refs, добавить GrainKeyType+StatesLookup stubs
- [x] 4. Восстановить Tools/DeploySetup и Tools/Generators (реальный генератор)
- [x] 5. Вернуть Cluster/Configs/InfrastructureConfigsState + AddConfigs
- [x] 6. Удалить ServiceTag.Game + GameServerOverview
- [x] 7. Создать Frontend Server/Client/Shared (WASM, без InteractiveAuto)
- [x] 8. Переименовать csproj Frontend.* → без префикса (Client/Server/Shared в slnx)
- [x] 9. Добавить MinIO пакет + ObjectStorage + EnsureBucket
- [x] 10. Добавить SoundCloudExplode 1.6.7 + InitializeAsync
- [x] 11. Портировать Meta/Audio/{Playlists,Songs} на StateCollection паттерн
- [x] 12. Портировать Meta/Images/ImagesCollection
- [x] 13. Создать Meta/Online (OnlineTracker/Listener) — зарегистрирован, использование отложено
- [x] 14. Console: Home+Radio pages на BlazorBlueprint (MudBlazor оставлен только как fallback для razor pages)
- [x] 15. MetaGateway/RadioEndpoints (playlists/songs/images/stream)
- [x] 16. Frontend Client — полная вёрстка радио‑плеера (ImagesView/AudioPlayer/SongDataView/ControlsView/StartWait) с WASM SessionState
- [x] 17. PlaylistDetail — кнопка Load songs с progress логом
- [x] 18. Console: удалить GAME секцию и лишние Status плитки

## Отложено

- `Meta/Online/OnlineTracker` — не используется, будет переделан на middleware "user activity last-seen" (раз в 5 минут)
- Миграция остальных razor MudBlazor → BlazorBlueprint (сейчас Console.csproj тянет оба)
- Минимальный JS interop тест в браузере (audio autoplay policy)
