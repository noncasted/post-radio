using Orleans.Transactions.Abstractions;

namespace Common;

public static class StateExtensions
{
    extension<T>(ITransactionalState<T> state) where T : class, new()
    {
        public Task<T> Update(Action<T> action)
        {
            return state.PerformUpdate(s =>
                {
                    action(s);
                    return s;
                }
            );
        }

        public Task Write(Action<T> action)
        {
            return state.PerformUpdate(action);
        }

        public Task<T> Read()
        {
            return state.PerformRead(s => s);
        }
    }
}