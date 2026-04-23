# Errors & Fixes: Quick Lookup Table

Быстрый справочник типовых проблем в backend-проекте.

---

## Runtime / Memory

| Симптом | Причина | Как чинить |
|---------|---------|------------|
| `/songs Load data` стирает длительность или `Fetch` возвращает массовый Unknown | Song metadata import/fetch перетёр одно поле из одного источника поверх другого | Используй `MetaDataCache` + `SongMetadataMerge`, не обновляй `SongState` из lookup/fetch вслепую |
| `ArgumentNullException` в `Advise`/`View`/`ListenQueue` | `null` вместо Lifetime | Передай реальный Lifetime — см. [COMMON_LIFETIMES.md](COMMON_LIFETIMES.md) |
| Callback срабатывает после удаления элемента коллекции | Подписка привязана к родительскому lifetime, а не к `item.Lifetime` | Используй `item.Lifetime` или `parent.Child()` |
| ViewableProperty View не даёт начального значения | Используется `Advise()` вместо `View()` | `View` = initial + future, `Advise` = future only |
| Observer callback блокирует всю grain-очередь на 50+ сек | Grain с `_observer` не `[Reentrant]` + dead-client | См. CLAUDE_MISTAKES.md lesson 9: `[Reentrant]` + `WaitAsync(timeout)` + discard observer на ошибке |
| После `MarkCoordinatorReady()` своё же состояние читается 55 сек | Сам-себя ждёшь через только что стартовавший Orleans-клиент | Не опрашивай после локального `await`; см. CLAUDE_MISTAKES.md lesson 10 |

---

## Orleans / State

| Симптом | Причина | Как чинить |
|---------|---------|------------|
| `[Id(N)]` конфликт при десериализации | Пропуск в нумерации или повторение | Сделай [Id] последовательными без разрывов; см. [COMMON_ORLEANS.md](COMMON_ORLEANS.md) |
| State не загружается / падает на чтении | Нет записи в `StatesLookup` или в `AddStates()` | 3 шага регистрации в COMMON_ORLEANS §State |
| `StateCollection` не синхронизируется | Нет `builder.AddStateCollection<...>()` | Зарегистрируй через `ProjectsSetupExtensions` |
| jsonb payload не читается | Забыт ведущий байт `0x01` | Используй `PostgresJsonbConverter<T>`, а не писать в jsonb напрямую |
| `[Transaction]` атрибут — непонятно где ставить | На интерфейсе grain, только если вызывается внутри `InTransaction(...)` | См. COMMON_ORLEANS §Grain Pattern |

---

## Tests (xUnit v3 / MS Testing Platform)

| Симптом | Причина | Как чинить |
|---------|---------|------------|
| `--filter "FullyQualifiedName~..."` игнорируется | Это синтаксис xUnit v2, убран в v3 | `-- --filter-class "*BoardTests"` (важен `--` разделитель) |
| `TestResults/*.log` нечитаемый (mojibake) | xUnit v3 пишет UTF-16LE | `tools/scripts/get-test-log.sh` → открывать `*.utf8.log` |
| Логи не создаются после `dotnet test` | Нет `UseMicrosoftTestingPlatformRunner` в csproj | Добавить `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>` + `<OutputType>Exe</OutputType>` |
| `xunit.runner.json` игнорируется | Не копируется в output | `<Content Include="xunit.runner.json" CopyToOutputDirectory="PreserveNewest"/>` |
| Тесты виснут | Deadlock / infinite loop / bad SQL / `Task.Delay` как синхронизация | Найти корень в коде, не увеличивать таймауты |
| Orphan Lifetime при падении теста | `new Lifetime()` вместо `handle.Lifetime` | Всегда `handle.Lifetime` — см. CLAUDE_MISTAKES lesson "new Lifetime() in Tests" |

---

## Deploy / Infrastructure

| Симптом | Причина | Где смотреть |
|---------|---------|--------------|
| `aspire run` грузит Debug-сборки (~870 MB) | Баг Aspire #13659 | Это dev-only; prod собирается Release через Dockerfile |
| В prod не работает то, что работало локально | В prod `Aspire/Program.cs` вообще не запускается | См. [DEPLOY.md](DEPLOY.md) — правь `docker-compose.yaml` / `Dockerfile` |
| PgBouncer поведение отличается dev ↔ prod | Dev — Aspire `AddContainer` sidecar, prod — first-class compose service | [DEPLOY.md](DEPLOY.md) |
| Миграции не применились | В prod нужен отдельный init-container `migrator` (DeploySetup) | [DEPLOY.md](DEPLOY.md) + [DEPLOY_TROUBLESHOOTING.md](DEPLOY_TROUBLESHOOTING.md) |

---

## Build / .csproj

| Симптом | Причина | Как чинить |
|---------|---------|------------|
| Новый .cs не компилируется | (Обычно **не** проблема — у нас SDK-style, glob авто-подхватывает) | Проверь, что файл не под `<Compile Remove>`; что путь не исключён в `Directory.Build.props` |
| `Newtonsoft.Json` vs `System.Text.Json` путаница | Orleans требует Newtonsoft — `OrleansJsonSerializer` / `JsonUtils.Settings` | Используй `JsonUtils.Settings` (`backend/Common/Extensions/JsonUtils.cs`) |
| Blazor `@inject` не работает | Директива `@inject` в разметке | Только `[Inject]` в `@code` блоке — см. [BLAZOR.md](BLAZOR.md) §Rule 2 |

---

## Reactive / Messaging (backend)

| Симптом | Причина | Как чинить |
|---------|---------|------------|
| Подписчик queue не получает события | Очередь не транзакционная, а пушим `PushTransactionalQueue` (или наоборот) | Согласуй тип push и listen |
| `ListenQueue` теряется после рестарта кластера | Привязан к глобальному lifetime, а не `DeployLifetime` | Подпишись в `IDeployAware.OnDeployChanged`, используй `deployLifetime` |
| Blazor страница не обновляется при изменении состояния | Используется `Advise` без `InvokeAsync(StateHasChanged)` | См. [BLAZOR.md](BLAZOR.md) §Rule 4 |

---

## Использование таблицы

1. Нашёл симптом в первой колонке.
2. Прочитал причину.
3. Перешёл по ссылке в «Как чинить» за деталями.

Если паттерн новый — добавь строку и обнови [CLAUDE_MISTAKES.md](CLAUDE_MISTAKES.md).
