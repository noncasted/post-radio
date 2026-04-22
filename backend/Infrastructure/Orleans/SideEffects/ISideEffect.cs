namespace Infrastructure;

public interface ISideEffect
{
    Task Execute(IOrleans orleans);
}

// Marker interface. Implementations are executed inside Transactions.Process().
// Deletion from side_effects_processing is atomic with the transaction's Postgres commit.
public interface ITransactionalSideEffect : ISideEffect
{
}