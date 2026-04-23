using Common;
using Infrastructure;
using Infrastructure.State;
using Orleans.Concurrency;

namespace Tests.Grains;

// --- Simple state test grain ---

[GenerateSerializer]
[GrainState(Table = "state_simple_test", State = "simple_test", Lookup = "SimpleTest", Key = GrainKeyType.Guid)]
public class SimpleTestState : IStateValue
{
    [Id(0)] public int Counter { get; set; }
    [Id(1)] public string Label { get; set; } = string.Empty;
    public int Version => 0;
}

public interface ISimpleTestGrain : IGrainWithGuidKey
{
    Task SetCounter(int value);
    Task<int> GetCounter();
    Task SetLabel(string label);
    Task<string> GetLabel();
}

public class SimpleTestGrain : Grain, ISimpleTestGrain
{
    public SimpleTestGrain([State] State<SimpleTestState> state)
    {
        _state = state;
    }

    private readonly State<SimpleTestState> _state;

    public async Task SetCounter(int value)
    {
        await _state.Write(s => s.Counter = value);
    }

    public async Task<int> GetCounter()
    {
        return await _state.Read(s => s.Counter);
    }

    public async Task SetLabel(string label)
    {
        await _state.Write(s => s.Label = label);
    }

    public async Task<string> GetLabel()
    {
        return await _state.Read(s => s.Label);
    }
}

// --- Collection test state ---

[GenerateSerializer]
[GrainState(Table = "state_test_collection", State = "collection_test", Lookup = "CollectionTest",
    Key = GrainKeyType.Guid)]
public class CollectionTestState : IStateValue
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
    public int Version => 0;
}

public interface ICollectionTestGrain : IGrainWithGuidKey
{
    Task SetName(string name);
    Task<string> GetName();
}

public class CollectionTestGrain : Grain, ICollectionTestGrain
{
    public CollectionTestGrain([State] State<CollectionTestState> state)
    {
        _state = state;
    }

    private readonly State<CollectionTestState> _state;

    public async Task SetName(string name)
    {
        await _state.Write(s => {
            s.Id = this.GetGrainId().GetGuidKey();
            s.Name = name;
        });
    }

    public async Task<string> GetName()
    {
        return await _state.Read(s => s.Name);
    }
}

// --- Transaction test grain ---

[GenerateSerializer]
[GrainState(Table = "state_tx_test", State = "tx_test", Lookup = "TxTest", Key = GrainKeyType.Guid)]
public class TxTestState : IStateValue
{
    [Id(0)] public int Value { get; set; }
    public int Version => 0;
}

public interface ITxTestGrain : IGrainWithGuidKey
{
    [Transaction]
    Task Increment();

    [Transaction]
    Task IncrementWithDelay(int delayMs);

    Task<int> Get();
    Task Deactivate();
}

[Reentrant]
public class TxTestGrain : Grain, ITxTestGrain
{
    public TxTestGrain([State] State<TxTestState> state)
    {
        _state = state;
    }

    private readonly State<TxTestState> _state;

    public Task Increment()
    {
        return _state.Write(s => s.Value += 1);
    }

    public async Task IncrementWithDelay(int delayMs)
    {
        await _state.Write(s => s.Value += 1);
        await Task.Delay(delayMs);
    }

    public async Task<int> Get()
    {
        return await _state.Read(s => s.Value);
    }

    public Task Deactivate()
    {
        DeactivateOnIdle();
        return Task.CompletedTask;
    }
}

// --- Side effect test grain ---

[GenerateSerializer]
public class TestSideEffect : ISideEffect
{
    [Id(0)] public Guid TargetGrainId { get; set; }

    public async Task Execute(IOrleans orleans)
    {
        var grain = orleans.GetGrain<ITxTestGrain>(TargetGrainId);
        var result = await orleans.Transactions.Run(() => grain.Increment());

        if (!result.IsSuccess)
            throw new Exception("Side effect transaction failed");
    }
}

// Transactional side effect — executes inside transaction, deletion atomic with commit
[GenerateSerializer]
public class TransactionalTestSideEffect : ITransactionalSideEffect
{
    [Id(0)] public Guid TargetGrainId { get; set; }

    public async Task Execute(IOrleans orleans)
    {
        var grain = orleans.GetGrain<ITxTestGrain>(TargetGrainId);
        await grain.Increment();
    }
}

// Side effect that fails N times before succeeding
[GenerateSerializer]
public class FailingTestSideEffect : ISideEffect
{
    [Id(0)] public Guid TargetGrainId { get; set; }
    [Id(1)] public int FailCount { get; set; }

    // Static counter shared across executions (keyed by TargetGrainId to avoid cross-test interference)
    internal static readonly object AttemptsLock = new();
    internal static readonly Dictionary<Guid, int> AttemptsDict = new();

    public async Task Execute(IOrleans orleans)
    {
        lock (AttemptsLock)
        {
            AttemptsDict.TryGetValue(TargetGrainId, out var count);
            AttemptsDict[TargetGrainId] = count + 1;

            if (count < FailCount)
                throw new Exception($"Intentional failure {count + 1}/{FailCount}");
        }

        var grain = orleans.GetGrain<ITxTestGrain>(TargetGrainId);
        var result = await orleans.Transactions.Run(() => grain.Increment());

        if (!result.IsSuccess)
            throw new Exception("Side effect transaction failed");
    }

    public static void ResetAttempts()
    {
        lock (AttemptsLock)
        {
            AttemptsDict.Clear();
        }
    }

    public static int GetAttemptCount(Guid targetId)
    {
        lock (AttemptsLock)
        {
            return AttemptsDict.GetValueOrDefault(targetId);
        }
    }
}

// Transactional side effect that fails N times before succeeding (uses shared FailingTestSideEffect counter)
[GenerateSerializer]
public class FailingTransactionalTestSideEffect : ITransactionalSideEffect
{
    [Id(0)] public Guid TargetGrainId { get; set; }
    [Id(1)] public int FailCount { get; set; }

    public async Task Execute(IOrleans orleans)
    {
        lock (FailingTestSideEffect.AttemptsLock)
        {
            FailingTestSideEffect.AttemptsDict.TryGetValue(TargetGrainId, out var count);
            FailingTestSideEffect.AttemptsDict[TargetGrainId] = count + 1;

            if (count < FailCount)
                throw new Exception($"Intentional transactional failure {count + 1}/{FailCount}");
        }

        var grain = orleans.GetGrain<ITxTestGrain>(TargetGrainId);
        await grain.Increment();
    }
}

// Side effect that always fails — for max retry tests
[GenerateSerializer]
public class AlwaysFailingSideEffect : ISideEffect
{
    [Id(0)] public Guid TrackingId { get; set; }

    public Task Execute(IOrleans orleans)
    {
        throw new Exception("Always fails");
    }
}

public interface ISideEffectTestGrain : IGrainWithGuidKey
{
    Task RegisterSideEffect(Guid targetId);
}

public class SideEffectTestGrain : Grain, ISideEffectTestGrain
{
    public SideEffectTestGrain(
        [State] State<TxTestState> state,
        ISideEffectsStorage sideEffectsStorage)
    {
        _state = state;
        _sideEffectsStorage = sideEffectsStorage;
    }

    private readonly State<TxTestState> _state;
    private readonly ISideEffectsStorage _sideEffectsStorage;

    public async Task RegisterSideEffect(Guid targetId)
    {
        await _sideEffectsStorage.Write(new TestSideEffect { TargetGrainId = targetId });
    }
}

// --- Grain that registers side effects within a transaction via AddToTransaction ---

public interface ITxSideEffectGrain : IGrainWithGuidKey
{
    [Transaction]
    Task IncrementAndRegisterSideEffect(Guid sideEffectTargetId);

    [Transaction]
    Task RegisterMultipleSideEffects(IReadOnlyList<Guid> targetIds);

    [Transaction]
    Task<int> IncrementAndReturn();

    Task<int> Get();
}

[Reentrant]
public class TxSideEffectGrain : Grain, ITxSideEffectGrain
{
    public TxSideEffectGrain([State] State<TxTestState> state)
    {
        _state = state;
    }

    private readonly State<TxTestState> _state;

    public async Task IncrementAndRegisterSideEffect(Guid sideEffectTargetId)
    {
        await _state.Write(s => s.Value += 1);
        new TestSideEffect { TargetGrainId = sideEffectTargetId }.AddToTransaction();
    }

    public async Task RegisterMultipleSideEffects(IReadOnlyList<Guid> targetIds)
    {
        await _state.Write(s => s.Value += 1);

        foreach (var targetId in targetIds)
            new TestSideEffect { TargetGrainId = targetId }.AddToTransaction();
    }

    public async Task<int> IncrementAndReturn()
    {
        var state = await _state.Update(s => s.Value += 1);
        return state.Value;
    }

    public async Task<int> Get()
    {
        return await _state.Read(s => s.Value);
    }
}