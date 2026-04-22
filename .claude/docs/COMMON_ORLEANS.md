# Orleans: Full Reference

## Quick Navigation

| Looking for... | Go to section |
|----------------|---------------|
| Grain interface + class boilerplate | Grain Pattern |
| State<T>, IStateValue, operations | State |
| GetGrain, cross-grain calls | IOrleans Interface |
| Multiple items stored as a collection | StateCollection |
| Push updates to clients | Messaging |
| OnActivateAsync, OnDeactivateAsync | Grain Lifecycle |

---

## Grain Pattern

```csharp
// Interface — one key type only
public interface IMyGrain : IGrainWithGuidKey {
    [Transaction]  // only if called inside a transaction
    Task<string> GetValue();

    Task FireAndForget();  // no attribute if not transactional
}

// Implementation
public class MyGrain : Grain, IMyGrain {
    // Constructor injection — never [Inject] fields
    public MyGrain(
        [State] State<MyState> state,
        IOrleans orleans,
        ILogger<MyGrain> logger) {
        _state = state;
        _orleans = orleans;
        _logger = logger;
    }

    private readonly State<MyState> _state;
    private readonly IOrleans _orleans;
    private readonly ILogger<MyGrain> _logger;

    public async Task<string> GetValue() {
        var s = await _state.ReadValue();
        return s.Value;
    }
}
```

**Key types:**
- `IGrainWithGuidKey` — Guid-based identity (most common for aggregates)
- `IGrainWithStringKey` — string-based identity (named singleton-like services)

---

## State

### State class

```csharp
[GenerateSerializer]
public class MyState : IStateValue {
    [Id(0)] public Guid Id { get; set; }    // Id(N) sequential, no gaps
    [Id(1)] public string Name { get; set; } = string.Empty;
    public int Version => 0;                 // always required
}
```

### Injecting

```csharp
// In grain constructor — [State] attribute triggers IStateFactory
public MyGrain([State] State<MyState> state) { ... }
```

### Operations

```csharp
// Read + modify + write — returns updated value
var state = await _state.Update(s => {
    s.Name = name;
});

// Read + modify + write — returns void
await _state.Write(s => {
    s.Name = name;
});

// Read only — returns T
var state = await _state.ReadValue();

// Read + transform
var name = await _state.Read(s => s.Name);
```

### Adding New State — 3 Steps

**Step 1** — add entry to `backend/Common/Lookups/StatesLookup.cs`:
```csharp
public static readonly Info MyEntity = new() {
    TableName = "state_my_entity",    // DB table name
    StateName = "my_entity",          // discriminator in DB
    KeyType = GrainKeyType.Guid       // must match grain key type
};
// Also add to the All list at the bottom
```

**Step 2** — register in `ProjectsSetupExtensions.AddStates()`:
```csharp
Add<MyState>(StatesLookup.MyEntity);
```

**Step 3 (collections only)** — register StateCollection in service extension (see below).

---

## IOrleans Interface

Central access point to Orleans from non-grain code (gateways, services):

```csharp
public interface IOrleans {
    IClusterClient Client { get; }
    ITransactions Transactions { get; }
    IStateStorage StateStorage { get; }
    IStateSerializer Serializer { get; }
    ILogger Logger { get; }
}

// Extension methods (use these, not Client directly):
orleans.GetGrain<IMyGrain>(guid);          // IGrainWithGuidKey
orleans.GetGrain<IMyGrain>("key");         // IGrainWithStringKey
orleans.GetGrain<IMyGrain>();              // IGrainWithGuidKey with Guid.Empty (singletons)
orleans.GetGrains<IMyGrain>(listOfGuids); // batch lookup

// Run code inside a transaction
await orleans.InTransaction(async () => {
    await grainA.DoSomething();
    await grainB.DoSomething();  // both in same transaction
});
```

**Inside a grain** — use `GrainFactory` directly (injected as `IGrainFactory`).

---

## StateCollection

Use when you need a persistent collection of items addressable by key, with sync via messaging.

```csharp
// 1. State class (same IStateValue rules apply)
[GenerateSerializer]
public class MyItemState : IStateValue {
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
    public int Version => 0;
}

// 2. Interface
public interface IMyCollection : IStateCollection<Guid, MyItemState> { }

// 3. Implementation — minimal, base class does everything
public class MyCollection(StateCollectionUtils<Guid, MyItemState> utils)
    : StateCollection<Guid, MyItemState>(utils), IMyCollection;

// 4. Grain that writes to the collection
public class MyItem : Grain, IMyItem {
    public MyItem([State] State<MyItemState> state, IMyCollection collection) {
        _state = state;
        _collection = collection;
    }

    public async Task OnUpdated() {
        var state = await _state.ReadValue();
        await _collection.OnUpdatedTransactional(state.Id, state);  // inside transaction
        // or: await _collection.OnUpdated(state.Id, state);        // outside transaction
    }
}
```

### Registration

```csharp
// In service extension method:
builder.AddStateCollection<MyCollection, Guid, MyItemState>()
    .As<IMyCollection>();
```

### Usage (read access from services)

```csharp
var item = _collection[itemId];                    // dictionary access
var all = _collection.Values;                      // all items
_collection.Updated.Advise(lifetime, OnChange);    // subscribe to changes
```

`StateCollection` implements `IReadOnlyDictionary`, so it can be used directly as a dictionary.
It loads all existing items on startup (`ILocalSetupCompleted`) and stays in sync via messaging.

---

## Messaging

Used to push state updates to clients (or between backend services) when they subscribe to a queue.

```csharp
// Push update to all subscribers of a queue
await _messaging.PushTransactionalQueue(queueId, new MyUpdateMessage { ... });

// Subscribe to a queue (backend service or gateway)
await _messaging.ListenQueue<MyUpdateMessage>(lifetime, queueId, OnUpdate);

private void OnUpdate(MyUpdateMessage message) {
    // handle incoming update
}
```

**Lifetime** is used here — it exists in backend too, not only in client. Subscription lives as long as the lifetime is active.

### Message class requirements

```csharp
[GenerateSerializer]
public class MyUpdateMessage {
    [Id(0)] public Dictionary<Guid, MyItem> Updates { get; set; } = new();
    [Id(1)] public List<Guid> Removals { get; set; } = new();
}
```

**StateCollection uses messaging internally** — it listens to updates automatically. You only write messaging directly if building a custom sync mechanism.

---

## Grain Lifecycle

```csharp
// Called when grain is activated (first call after idle)
public override Task OnActivateAsync(CancellationToken cancellationToken) {
    _task.Delay = Options.Delay;
    return base.OnActivateAsync(cancellationToken);
}

// Called before grain is deactivated
public override async Task OnDeactivateAsync(
    DeactivationReason reason,
    CancellationToken cancellationToken) {
    // throwing prevents deactivation (keeps grain alive):
    if (DateTime.UtcNow - _lastActivity < TimeSpan.FromMinutes(3))
        throw new Exception("Keeping grain alive");

    await base.OnDeactivateAsync(reason, cancellationToken);
}
```

Use `OnActivateAsync` for: starting timers, loading supplementary data not in state.
Use `OnDeactivateAsync` for: flushing pending writes, preventing premature deactivation.

---

## Storage: PostgreSQL jsonb

Orleans state is persisted via custom `IGrainStateStorage` backed by PostgreSQL `jsonb` columns.

### Storage schema

Single table `OrleansStorage` holds all grain state:

| Column | Purpose |
|--------|---------|
| `GrainIdHash` | 32-bit unchecked hash of `GrainKey.GetHashBytes()` |
| `GrainIdN0`, `GrainIdN1` | Raw grain id bytes |
| `GrainTypeHash` | Hash of UTF-8 encoded type string |
| `GrainTypeString` | Full grain type name |
| `GrainIdExtensionString` | Extension segment of `GrainId` |
| `ServiceId` | Constant `"atlantis"` |
| `PayloadBinary` | jsonb binary (see below) |
| `ModifiedOn`, `Version` | Optimistic concurrency |

### jsonb binary format

PostgreSQL stores `jsonb` with a leading **version byte** (`0x01`). `PostgresJsonbConverter<T>` in `backend/Common/Extensions/` handles both sides:

- **Write**: prepend `0x01` to UTF-8 JSON → byte array → `jsonb` column
- **Read**: skip `0x01` → deserialize remaining bytes as UTF-8 JSON

Serialization itself uses `OrleansJsonSerializer` (Newtonsoft.Json with Orleans settings exposed via `JsonUtils.Settings`).

### GrainStateStorage API

```csharp
T? Read<T>(GrainId id);
string? ReadRaw<T>(GrainId id);          // raw JSON, no deserialization
Task Write<T>(GrainId id, T value);
Task Write(IReadOnlyList<StateWriteRequest> batch);  // foreach INSERT...ON CONFLICT
```

Implementation lives in `backend/Infrastructure/Orleans/State/GrainStateStorage.cs`, uses `NpgsqlDataSource` for database access.

---

## Key Files

| File | Purpose |
|------|---------|
| `backend/Common/Lookups/StatesLookup.cs` | All state info (table name, state name, key type) |
| `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs` | Registers state types in `AddStates()` |
| `backend/Infrastructure/Orleans/State/State.cs` | `State<T>` implementation |
| `backend/Infrastructure/Orleans/State/StateExtensions.cs` | `Update()`, `Write()`, `ReadValue()`, `Read()` helpers |
| `backend/Infrastructure/Data/Collections/StateCollection.cs` | `StateCollection<TKey, TValue>` base |
| `backend/Infrastructure/Orleans/Utils/OrleansUtils.cs` | `IOrleans` implementation + extension methods |
| `backend/Meta/Bots/BotEntity.cs` | Grain + State<T> example |
| `backend/Meta/Users/Entities/User.cs` | Grain + State<T> example |
| `backend/Meta/Bots/BotCollection.cs` | StateCollection example |
| `backend/Infrastructure/Orleans/State/GrainStateStorage.cs` | jsonb storage implementation |
| `backend/Common/Extensions/PostgresJsonbConverter.cs` | jsonb read/write converter |
| `backend/Common/Extensions/JsonUtils.cs` | Shared Newtonsoft.Json settings |
