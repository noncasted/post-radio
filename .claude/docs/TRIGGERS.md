# Trigger Keywords → Documentation

Быстрый поиск документации по ключевому слову. Используй при старте задачи.

---

## Orleans / Grains / State

**Keywords:** grain, IGrainWithGuidKey, IGrainWithStringKey, State<T>, IStateValue, [GenerateSerializer], [Id(N)], [Transaction], StatesLookup, AddStates, StateCollection, IOrleans, GetGrain, InTransaction, GrainStateStorage, jsonb, PostgresJsonbConverter, OrleansStorage, OnActivateAsync, OnDeactivateAsync, [Reentrant], observer

→ [COMMON_ORLEANS.md](COMMON_ORLEANS.md)

---

## Messaging / Reactive (backend)

**Keywords:** IMessaging, PushTransactionalQueue, ListenQueue, EventSource, LifetimedValue, ViewableProperty, ViewableList, ViewableDictionary, StateCollection.Updated, LiveState, Advise, View

→ [COMMON_ORLEANS.md](COMMON_ORLEANS.md) §Messaging + [COMMON_LIFETIMES.md](COMMON_LIFETIMES.md)
→ Код: `backend/Common/Reactive/`

---

## Lifetime

**Keywords:** Lifetime, IReadOnlyLifetime, Terminate, Child, Intersect, TerminatedLifetime, DeployLifetime, resource scope, cleanup, disposal

→ [COMMON_LIFETIMES.md](COMMON_LIFETIMES.md)

---

## Deploy Epoch

**Keywords:** DeployId, IDeployManagement, IDeployContext, IDeployAware, DeployIdPipe, DeployIdentity, DeployLifetime, LiveState, IServiceDiscoveryStorage, DeployHealthChecker, IDeployCleanup, cluster restart, coordinator ready, epoch

→ [DEPLOY.md](DEPLOY.md) + [DEPLOY_TROUBLESHOOTING.md](DEPLOY_TROUBLESHOOTING.md)

---

## Aspire / Docker / Coolify

**Keywords:** aspire run, Aspire AppHost, dcp, docker-compose, Coolify, Traefik, PgBouncer, DbUpstreamFactory, PgBouncerFactory, migrator, init-container, Dockerfile, DeploySetup, PostResourcesSetup

→ [DEPLOY.md](DEPLOY.md) — архитектура dev↔prod
→ [DEPLOY_TROUBLESHOOTING.md](DEPLOY_TROUBLESHOOTING.md) — проблемы при разворачивании

---

## Blazor Frontend Console

**Keywords:** Blazor, .razor, @inject, [Inject], UiComponent, ComponentBase, InvokeAsync, StateHasChanged, [Parameter, EditorRequired], LucideIcon, BbAlert, ToastService, NavigationManager, early return

→ [BLAZOR.md](BLAZOR.md)

---

## Tests (xUnit v3)

**Keywords:** xUnit v3, Microsoft Testing Platform, UseMicrosoftTestingPlatformRunner, xunit.runner.json, filter-class, filter-method, filter-namespace, ITestOutputHelper, TestResults, UTF-16LE, get-test-log, ClusterTestRoot, handle.Lifetime, benchmark

→ [TESTING.md](TESTING.md)

---

## Telemetry / Logs / Metrics

**Keywords:** .telemetry, telemetry, metrics, MetricsSnapshotService, FileLoggerProvider, SessionFileLogger, TelemetryPaths, logs-games, OpenTelemetry, ServiceDefaultsExtensions

→ [TELEMETRY.md](TELEMETRY.md)

---

## Code Style

**Keywords:** member order, _camelCase, naming, TryGetValue, braces, inline init, collection init

→ [CODE_STYLE_FULL.md](CODE_STYLE_FULL.md)

---

## Vocabulary

**Keywords:** terminology, naming convention, synonyms, Advise vs View

→ [VOCABULARY.md](VOCABULARY.md)

---

## Self-Learning Log

**Keywords:** past mistake, lesson, anti-pattern, recurring error

→ [CLAUDE_MISTAKES.md](CLAUDE_MISTAKES.md)
