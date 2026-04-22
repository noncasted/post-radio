## SoundCloud tunnel — Результат

### Статус: Завершено для локального репозитория

### Что сделано
- Удалён ручной SOCKS5 tunnel-код: `Socks5ConnectCallback` больше не используется и файл удалён.
- Убраны диагностические tunnel-логи из `AudioServicesExtensions`, `AudioServicesStartup` и `PlaylistLoader.Fetch`.
- Убран форвардинг `Audio__Socks5Proxy` из dev-only Aspire AppHost: сервисы снова запускаются без tunnel-env.
- Удалены deploy-артефакты `sc-tunnel`: compose, Dockerfile, entrypoint, README, `.env.example` и secrets-каталог.
- Удалён `backend/Tools/DeploySetup/secrets.local.json` с tunnel-кредами.
- Из `.gitignore` убраны tunnel-specific паттерны `**/secrets.local.json` и `**/secrets/`.
- `Audio.Socks5Proxy` оставлен как null-by-default аварийный выключатель без sidecar/туннельной инфраструктуры.

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `.gitignore` | Удалены tunnel-specific ignore-паттерны для secrets. |
| `backend/Meta/Audio/AudioOptions.cs` | Комментарий к `Socks5Proxy` очищен от упоминаний `sc-tunnel`/prod sidecar. |
| `backend/Meta/Audio/AudioServicesExtensions.cs` | Удалены `Console.WriteLine`, logger-диагностика и ручной `ConnectCallback`; оставлен прямой handler по умолчанию. |
| `backend/Meta/Audio/AudioServicesStartup.cs` | Убраны tunnel-диагностика и try/catch вокруг `SoundCloudClient.InitializeAsync`. |
| `backend/Meta/Audio/Playlists/PlaylistLoader.cs` | Убраны tunnel-диагностика и try/catch вокруг `GetTracksAsync`. |
| `backend/Orchestration/Aspire/Program.cs` | Убран блок чтения/логирования/форвардинга `Audio:Socks5Proxy`. |
| `backend/Meta/Audio/Socks5ConnectCallback.cs` | Удалён файл. |
| `backend/Tools/deploy/` | Удалена директория tunnel-deploy артефактов. |
| `backend/Tools/DeploySetup/secrets.local.json` | Удалён файл с tunnel-секретами. |
| `docs/tasks/current/soundcloud-tunnel/soundcloud-tunnel_progress.md` | Добавлены рабочие заметки по откату и проверке. |
| `docs/tasks/current/soundcloud-tunnel/soundcloud-tunnel_result.md` | Добавлен итог задачи. |

### Проверка
- `dotnet build backend/post-radio.slnx --no-restore` — успешно, 0 ошибок.
- Осталось существующее предупреждение `CS4014` в `backend/Meta/Images/ImagesCollection.cs(41,9)`, не связано с tunnel-откатом.
- `rg` по backend/.gitignore на `ConnectCallback`, `sc-tunnel`, `Audio__Socks5Proxy`, `Shadowsocks`, `Xray`, `Amnezia`, `awg`, `tunnel` — совпадений не осталось.

### Отличия от плана
- `backend/Tools/deploy/README.md` не обновлялся, а удалён вместе со всей `backend/Tools/deploy/`, потому что после отказа от тоннеля в этой директории не осталось нетуннельных артефактов.

### Нерешенные вопросы
- Внешняя очистка VPS `51.68.143.178` и AmneziaVPN peer не выполнялась из этой сессии: это credential-gated/external-production/destructive действие. Локальный репозиторий очищен от tunnel-кода и tunnel-секретов.
