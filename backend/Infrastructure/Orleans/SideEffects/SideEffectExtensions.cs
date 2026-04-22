namespace Infrastructure;

public static class SideEffectExtensions
{
    public static void AddToTransaction(this ISideEffect sideEffect)
    {
        if (TransactionContextProvider.Current == null)
            throw new InvalidOperationException("Side effects can only be registered within a transaction.");

        TransactionContextProvider.Current.SideEffects.TryAdd(Guid.NewGuid(), sideEffect);
    }
}