using Common;

namespace Infrastructure;

[GrainState(Table = "configs", State = "transaction_config", Lookup = "TransactionConfig", Key = GrainKeyType.String)]
public class TransactionOptions
{
    public float LockWaitSeconds { get; set; } = 3f; // how long to wait for another transaction to release the lock

    public float StuckGraceSeconds { get; set; } =
        30f; // grace period before a non-responsive transaction is considered stuck
}

public interface ITransactionConfig : IAddressableState<TransactionOptions>
{
}