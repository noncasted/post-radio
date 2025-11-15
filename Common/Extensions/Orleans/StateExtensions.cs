using Orleans.Transactions.Abstractions;

namespace Common;

public static class StateExtensions
{
    public static Task<T> Update<T>(this ITransactionalState<T> state, Action<T> action) where T : class, new()
    {
        return state.PerformUpdate(s =>
        {
            action(s);
            return s;
        });
    }
    
    public static Task Write<T>(this ITransactionalState<T> state, Action<T> action) where T : class, new()
    {
        return state.PerformUpdate(action);
    }

    public static Task<T> Read<T>(this ITransactionalState<T> state) where T : class, new()
    {
        return state.PerformRead(s => s);
    }
}