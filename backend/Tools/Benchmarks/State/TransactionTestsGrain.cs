using Common;
using Infrastructure;
using Infrastructure.State;

namespace Benchmarks;

[GenerateSerializer]
[GrainState(Table = "state_test_transactional_state", State = "transaction_test", Lookup = "TransactionTest",
    Key = GrainKeyType.Guid)]
public class TransactionTestState : IStateValue
{
    [Id(0)]
    public int Value { get; set; }

    public int Version => 0;
}

public interface ITransactionTestGrain : IGrainWithGuidKey
{
    [Transaction]
    Task Increment();

    [Transaction]
    Task IncrementWithDelay(int delayMs);

    Task<int> Get();
    Task Deactivate();
}

[GrainType("bench-transaction-test")]
public class TransactionTestGrain : Grain, ITransactionTestGrain
{
    public TransactionTestGrain([State] State<TransactionTestState> state)
    {
        _state = state;
    }

    private readonly State<TransactionTestState> _state;

    public Task Increment()
    {
        return _state.Write(s => {
            s.Value += 1;
        });
    }

    public async Task IncrementWithDelay(int delayMs)
    {
        await _state.Write(s => {
            s.Value += 1;
        });
        await Task.Delay(delayMs);
    }

    public async Task<int> Get()
    {
        await _state.Read();
        return _state.Value.Value;
    }

    public Task Deactivate()
    {
        DeactivateOnIdle();
        return Task.CompletedTask;
    }
}