# Claude Code Instructions

## Overview

Backend-only проект на **.NET 10 + Orleans + Aspire (dev) / Docker Compose + Coolify (prod) + PostgreSQL + Blazor console**.

Структура репозитория:
- `backend/Cluster/` — deploy epoch, coordination, service discovery, diagnostics, monitoring, state
- `backend/Common/` — общие утилиты: `Reactive/` (Lifetime, EventSource, ViewableProperty, ViewableList/Dictionary), `Storage/`, `Extensions/` (включая `PostgresJsonbConverter`, `JsonUtils`), `Lookups/` (StatesLookup)
- `backend/Infrastructure/` — Orleans-инфраструктура (State, GrainStateStorage, Utils/OrleansUtils), Metrics, Logging, Data/Collections (StateCollection)
- `backend/Meta/` — доменные grains (Users, Bots, Online, Images, ...)
- `backend/Orchestration/` — `Aspire/` (dev-only AppHost), `Silo/`, `ConsoleGateway/`, `Extensions/` (`ProjectsSetupExtensions.AddStates()`)
- `backend/Frontend/` — Blazor: `Client/`, `Server/`, `Shared/`
- `backend/Console/` — консольная админка (Blazor razor pages)
- `backend/Tools/` — `Tests/` (xUnit v3 + MS Testing Platform), `Benchmarks/`, `deploy/` (Dockerfile + docker-compose.yaml)
- `tools/` — скрипты (`get-test-log.sh`), playground
- `.telemetry/` (gitignored) — metrics, logs, logs-games

## Keyword → Documentation (FAST LOOKUP)

| Keywords | Go to |
|----------|-------|
| grain, IGrainWithGuidKey, [Transaction], State\<T\>, IStateValue, [GenerateSerializer], [Id(N)], StatesLookup, AddStates, StateCollection, IOrleans, GetGrain, InTransaction, OnActivateAsync, OnDeactivateAsync, [Reentrant] | docs/COMMON_ORLEANS.md |
| messaging, IMessaging, PushTransactionalQueue, ListenQueue, queue subscription | docs/COMMON_ORLEANS.md §Messaging |
| jsonb, GrainStateStorage, PostgresJsonbConverter, OrleansStorage, PostgreSQL payload | docs/COMMON_ORLEANS.md §Storage |
| Lifetime, IReadOnlyLifetime, Advise, View, Terminate, Child, Intersect, TerminatedLifetime, DeployLifetime, memory leak | docs/COMMON_LIFETIMES.md |
| EventSource, LifetimedValue, ViewableProperty, ViewableList, ViewableDictionary, reactive, observable | docs/COMMON_LIFETIMES.md + `backend/Common/Reactive/` |
| DeployId, IDeployManagement, IDeployContext, IDeployAware, DeployIdPipe, DeployIdentity, LiveState, IServiceDiscoveryStorage, DeployHealthChecker, cluster restart, coordinator ready, epoch | docs/DEPLOY.md |
| aspire run, Aspire AppHost, dcp, docker-compose, Coolify, Traefik, PgBouncer, DbUpstreamFactory, PgBouncerFactory, migrator, Dockerfile, DeploySetup, PostResourcesSetup | docs/DEPLOY.md |
| deploy troubleshooting, prod broken, compose logs, pgbouncer error, migration failed | docs/DEPLOY_TROUBLESHOOTING.md |
| Blazor, razor, @inject, [Inject], UiComponent, ComponentBase, InvokeAsync, StateHasChanged, [Parameter], ToastService, LucideIcon, BbAlert, early return | docs/BLAZOR.md |
| xUnit v3, Microsoft Testing Platform, UseMicrosoftTestingPlatformRunner, xunit.runner.json, filter-class, filter-method, ITestOutputHelper, TestResults, UTF-16LE, get-test-log, ClusterTestRoot, handle.Lifetime | docs/TESTING.md |
| telemetry, metrics, logs, .telemetry, FileLoggerProvider, SessionFileLogger, MetricsSnapshotService, TelemetryPaths, logs-games, OpenTelemetry | docs/TELEMETRY.md |
| member order, _camelCase, TryGetValue, braces, inline init, code style | docs/CODE_STYLE_FULL.md |
| error lookup, why X fails, memory leak, build error, test error, deploy error | docs/ERRORS.md |
| terminology, naming, Advise vs View, primary terms | docs/VOCABULARY.md |
| common mistakes, past lessons, anti-patterns | docs/CLAUDE_MISTAKES.md |
| trigger keywords, documentation finder | docs/TRIGGERS.md |

## Архитектурная сводка

**Orleans grains**:
- Состояние через `State<T>` + `[State]` constructor injection, типы реализуют `IStateValue` с `[GenerateSerializer]` и последовательными `[Id(N)]`.
- Регистрация state в 3 шага: `StatesLookup` → `AddStates()` → (для коллекций) `AddStateCollection<...>()`.
- Транзакции — через `IOrleans.InTransaction(...)`; `[Transaction]` атрибут — только на методах интерфейса, вызываемых внутри транзакции.
- Grain, форвардящий на observer клиента, — обязательно `[Reentrant]` + `WaitAsync(timeout)` + discard observer на ошибке.

**Reactive (backend)**:
- `EventSource<T>`, `LifetimedValue<T>`, `ViewableProperty<T>`, `ViewableList<T>`, `ViewableDictionary<K,V>` живут в `backend/Common/Reactive/`.
- Любая подписка (`Advise`, `View`, `ListenQueue`) требует `Lifetime`, иначе утечка.
- Epoch-scoped подписки — на `DeployLifetime` из `IDeployContext`.

**Blazor**:
- `@inject` только в `@code` через `[Inject]`, не директивой.
- Страницы с реактивной подпиской наследуются от `UiComponent` — получают `OnSetup(IReadOnlyLifetime)`.
- Состояния грузятся с `early return` паттерном; коллекции рендерятся через отдельный компонент.

**Deploy**:
- Dev и prod **не делят runtime**. Dev — `aspire run` (`backend/Orchestration/Aspire/Program.cs`), prod — Docker Compose под Coolify (`backend/Tools/deploy/`).
- Prod **никогда** не запускает `Aspire/Program.cs`.
- Миграции в prod — отдельный `migrator` init-container.

## Critical Rules

1. **Не лезь внутрь `/bin` и `/obj`** — только исходники.
2. **Ответы и комментарии**: проза — по-русски, идентификаторы/код — по-английски.
3. **Без эмодзи** в коде и доках (в т.ч. ✅/❌ — используй `OK:` / `WRONG:` или `[+]/[-]`).
4. **UTF-8**, в том числе для C#. Тестовые логи xUnit v3 — UTF-16LE, конвертируй через `tools/scripts/get-test-log.sh` перед чтением.
5. **.csproj — SDK-style**: новые `.cs` подхватываются glob-ом автоматически, вручную `<Compile Include>` **не нужен**. Если файл не попадает в сборку — проверь `Directory.Build.props` и `<Compile Remove>` в самом csproj.
6. **Не смешивай dev и prod deploy** — правка в `Aspire/Program.cs` не влияет на prod; правка в `docker-compose.yaml` не влияет на dev. См. docs/DEPLOY.md.
7. **Никогда `new Lifetime()` в тестах** — только `handle.Lifetime` или `handle.Lifetime.Child()`. См. docs/CLAUDE_MISTAKES.md lesson 1.
8. **Observer-grain обязан быть `[Reentrant]`** + должен сбрасывать мёртвый observer. См. docs/CLAUDE_MISTAKES.md lesson 2.
9. **Обновляй knowledge base** — если нашёл новый anti-pattern, добавь в `docs/CLAUDE_MISTAKES.md` + `docs/ERRORS.md`.
10. **При компакции контекста** сохраняй: Orleans grain pattern, правила Lifetime, список изменённых файлов, in-progress task state.

## Full Documentation

**Core:**
- `docs/COMMON_ORLEANS.md` — Grains, State\<T\>, StateCollection, IOrleans, messaging, jsonb storage
- `docs/COMMON_LIFETIMES.md` — Lifetime basics + advanced patterns (hierarchy, intersect, DeployLifetime)
- `docs/BLAZOR.md` — Blazor console UI правила
- `docs/DEPLOY.md` — архитектура dev↔prod deploy
- `docs/DEPLOY_TROUBLESHOOTING.md` — проблемы и обходы при разворачивании
- `docs/TESTING.md` — xUnit v3 / Microsoft Testing Platform workflow
- `docs/TELEMETRY.md` — `.telemetry/` структура, метрики, логи, session logs
- `docs/CODE_STYLE_FULL.md` — member order, naming, braces, Task vs ValueTask, nullable

**Reference:**
- `docs/ERRORS.md` — таблица ошибок с причинами и фиксами
- `docs/VOCABULARY.md` — единая терминология
- `docs/CLAUDE_MISTAKES.md` — лог накопленных уроков
- `docs/TRIGGERS.md` — keyword → doc (дублирует таблицу выше, с дополнительными контекстами)
