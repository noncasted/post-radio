# Vocabulary Index

Единая терминология. При сомнении — используй основной термин из этого списка.

## Reactive (backend)

| Concept | Primary Term | Aliases | File |
|---------|--------------|---------|------|
| Managed lifetime | Lifetime | IReadOnlyLifetime, scope lifetime | COMMON_LIFETIMES.md |
| Lifetime termination | Terminate | End, dispose, cleanup | COMMON_LIFETIMES.md |
| Parent-child lifetime | Lifetime hierarchy | Nesting | COMMON_LIFETIMES.md |
| Auto-cleanup subscription | Lifetime-scoped subscription | Auto-unsubscribe | COMMON_LIFETIMES.md |
| Fire event | Invoke | Emit, send, signal | — |
| Observable event | EventSource | Event stream | `backend/Common/Reactive/Events/` |
| Value + lifetime | LifetimedValue | State container | `backend/Common/Reactive/Events/` |
| Observable property | ViewableProperty | Reactive property | `backend/Common/Reactive/DataTypes/Properties/` |
| Observable list | ViewableList | Reactive list | `backend/Common/Reactive/DataTypes/Lists/` |
| Observable dictionary | ViewableDictionary | Reactive map | `backend/Common/Reactive/DataTypes/Dictionaries/` |
| Future-only subscription | Advise | Subscribe to future events | — |
| Immediate + future subscription | View | Subscribe with initial callback | — |

## Orleans

| Concept | Primary Term | Notes |
|---------|--------------|-------|
| Distributed actor | Grain | `IGrainWithGuidKey` / `IGrainWithStringKey` |
| Grain state | State<T> | Inject via `[State]` attribute, implements `IStateValue` |
| Cross-grain transaction | InTransaction | Use `orleans.InTransaction(...)` for atomicity |
| Transactional method | [Transaction] | Only on interface methods called inside InTransaction |
| Persistent collection | StateCollection<TKey, TValue> | Loads from DB, syncs via messaging |
| Orleans access facade | IOrleans | Central entry point — `GetGrain`, `InTransaction`, `Serializer` |
| Push update to subscribers | PushTransactionalQueue | Via `IMessaging` |
| Subscribe to queue | ListenQueue | Requires Lifetime |
| Grain activation callback | OnActivateAsync | First call after idle |
| Grain deactivation callback | OnDeactivateAsync | Throw to keep grain alive |
| Re-entrant grain | `[Reentrant]` | Required for grains forwarding to observers |
| Grain state storage | GrainStateStorage | PostgreSQL jsonb-backed |
| jsonb wrapper | PostgresJsonbConverter | Adds leading `0x01` version byte |

## Deploy Epoch

| Concept | Primary Term | Description |
|---------|--------------|-------------|
| Cluster deploy identity | DeployId | Random Guid на каждый старт координатора; ключ для epoch-scoped grains |
| Deploy source of truth | IDeployManagement | Persistent grain (key=DeployId) с LastHeartbeat + CoordinatorReady |
| Deploy members store | IServiceDiscoveryStorage | Persistent grain; `Update(overview)` = upsert + stale prune + snapshot |
| Deploy identity broadcaster | DeployIdentity | Coordinator-only hosted service: generates DeployId + heartbeat |
| Deploy identity pipe | DeployIdPipe | RuntimePipe: сервисы спрашивают текущий DeployId при старте |
| Scoped epoch lifetime | DeployLifetime | Дочерний lifetime внутри IDeployContext, терминируется на смене epoch |
| Epoch-aware component | IDeployAware | `OnDeployChanged(newId, lifetime)` — пересоздать подписки |
| Live ephemeral sync | LiveState\<T\> | Канал id включает DeployId, scope = DeployLifetime |
| Epoch watchdog | DeployHealthChecker | Hosted service, опрашивает pipe + heartbeat |
| Old-deploy cleanup | IDeployCleanup | Удаляет state rows для всех DeployId ≠ текущего |

## Aspire / Deploy

| Concept | Primary Term | Notes |
|---------|--------------|-------|
| Dev orchestrator | Aspire AppHost + dcp | `backend/Orchestration/Aspire/Program.cs` — **только dev** |
| Prod orchestrator | Docker Compose + Coolify Traefik | `backend/Tools/deploy/docker-compose.yaml` |
| PgBouncer (dev) | Aspire sidecar | `PgBouncerFactory` |
| PgBouncer (prod) | Compose service | First-class resource |
| Migrations (prod) | migrator init-container | `DeploySetup` |
| Migrations (dev) | PostResourcesSetup.Run | Вызывается из AppHost после старта ресурсов |

## Правила (не использовать синонимы)

- Не смешивать: `Lifetime` vs `Token` (token — часть lifetime, не синоним).
- Не смешивать: `Advise` vs `View` (разное поведение при старте).
- Не смешивать: `EventSource` vs `ViewableProperty` (событие vs состояние).
- Не смешивать: `State<T>` vs `StateCollection<K,V>` (один vs множество).
- Не смешивать: `Aspire` vs `Coolify` (dev vs prod orchestrator — разные среды).
- В одном ответе используй один и тот же термин.
