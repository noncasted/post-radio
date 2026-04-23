# Infrastructure Tests

Integration tests requiring Orleans TestCluster + PostgreSQL.

## Done

### State Read/Write — `State/StateReadWriteTests.cs` (4 tests)
- [x] Write and read single value
- [x] Write string label
- [x] Multiple writes keep latest
- [x] Different grains have independent state

### Transactions — `State/TransactionTests.cs` (4 tests)
- [x] Single increment commits
- [x] Multiple increments all applied
- [x] Rollback on exception — value unchanged
- [x] State persists after grain deactivation (reload from DB)

### Side Effects — `State/SideEffectTests.cs` (8 tests)
- [x] Register SE → drain → target grain updated
- [x] Multiple SEs → drain → all executed
- [x] Transactional SE (ITransactionalSideEffect) — executes inside transaction
- [x] Transactional SE — multiple drain, all executed
- [x] Retry on failure — fails N times, succeeds on N+1 attempt
- [x] Max retries exceeded — entry dropped after MaxRetryCount
- [x] RequeueStuck — crashed processing entries recovered back to queue
- [x] Empty queue — drain returns quiet with no work

### DurableQueue — `Messaging/DurableQueueTests.cs` (5 tests)
- [x] PushDirect single message delivered
- [x] PushDirect multiple messages all delivered
- [x] Multiple listeners all receive
- [x] Terminated listener — no delivery
- [x] PushTransactional — delivered after commit

### RuntimePipe — `Messaging/RuntimePipeTests.cs` (5 tests)
- [x] Send with handler returns response
- [x] Multiple requests each get correct response
- [x] No handler throws exception
- [x] Handler throws — error propagates
- [x] Async handler awaits correctly

### RuntimeChannel — `Messaging/RuntimeChannelTests.cs` (6 tests)
- [x] Single subscriber receives
- [x] Multiple subscribers all receive
- [x] Multiple messages delivered in order
- [x] Different channels isolated
- [x] Terminated listener — no delivery
- [x] No subscribers — does not throw

## Todo

### State Migrations
- [ ] Write V0 state → read as V1 via migration step
- [ ] Concurrent reads during migration

### StateCollection Sync
- [ ] OnUpdated pushes to collection
- [ ] OnUpdatedTransactional within transaction
- [ ] Collection reflects grain state changes

### Transactions — Advanced
- [ ] Chained transaction (multiple grains in one TX)
- [ ] Chained fail — all rollback
- [ ] Concurrent transactions on same grain
- [ ] Overlapping transactions — conflict resolution
- [ ] Mid-chain fail — partial rollback
- [ ] Large batch transaction (10+ grains)
- [ ] Empty transaction — no state changes
- [ ] Transaction takeover — stuck TX cleanup

### Side Effects — Advanced
- [ ] Feature flag disable — side effects paused (requires SideEffectsWorker, not pipeline)
- [ ] Dead letter handling for unparseable payloads (requires corrupt JSON in DB)
- [ ] Concurrent workers don't process same effect (SKIP LOCKED, requires multi-silo)

### State Storage & Caching
- [ ] StateStorageCache — SQL query caching correctness
- [ ] Read with extension filter — only matching entries returned
- [ ] ReadAll with large result set — async enumeration works
- [ ] Write then Read round-trip — data preserved
- [ ] Delete — entry removed from storage
- [ ] Version tracking — old version triggers migration

### State Migrations
- [ ] Write V0 → migrate to V1 — correct transformation
- [ ] Sequential migration V0 → V1 → V2
- [ ] GetLatestVersion returns highest version
- [ ] No migrations registered — returns original
- [ ] Type mismatch after migration — throws

### State Transactions — Advanced
- [ ] Concurrent transaction detection on same state — throws
- [ ] Transaction-scoped caching — reads within TX use cached value
- [ ] OnTransactionSuccess clears transaction ID
- [ ] OnTransactionFailure clears cached value
- [ ] State.Read inside vs outside transaction — different paths
- [ ] State.Write inside transaction — deferred until commit

### Dynamic State
- [ ] SetValue updates ViewableProperty
- [ ] SetValue publishes to RuntimeChannel
- [ ] Channel update from another instance — local value updated
- [ ] Write lock prevents concurrent SetValue

### Addressable State
- [ ] OnOrleansStarted loads persisted state from storage
- [ ] IsInitialized false before first SetValue
- [ ] SetValue persists to storage AND publishes to channel
- [ ] Cross-instance sync via channel updates

### Task Balancer
- [ ] Critical priority executes before Low priority
- [ ] Aging mechanism — older Low tasks eventually execute
- [ ] Concurrent execution limited by SemaphoreSlim
- [ ] Failed task re-enqueued with ExceptionPenalty reduction
- [ ] Empty queue — collect returns nothing

### Task Queue
- [ ] Enqueue with delay — not available before delay expires
- [ ] Enqueue without delay — available immediately
- [ ] Collect returns only ready tasks
- [ ] Thread-safe concurrent enqueue/collect

### Service Loop lifecycle
- [ ] OnOrleansStarted runs all lifecycle participants in parallel
- [ ] OnLocalSetupCompleted runs after Orleans started
- [ ] Setup handler failure — logged, does not block others
- [ ] Dependency ordering enforced (Orleans → Local → Coordinator)
