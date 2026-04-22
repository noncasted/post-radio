namespace Infrastructure.State;

public static class StateExtensions
{
    public static async Task Write<T>(this State<T> state, Action<T> action)
        where T : class, IStateValue, new()
    {
        await state.Read();
        action(state.Value);
        await state.Write();
    }

    public static async Task<T> Update<T>(this State<T> state, Action<T> action)
        where T : class, IStateValue, new()
    {
        await state.Read();
        action(state.Value);
        await state.Write();

        return state.Value;
    }

    public static async Task<T> ReadValue<T>(this State<T> state)
        where T : class, IStateValue, new()
    {
        await state.Read();
        return state.Value;
    }

    public static async Task<TResult> Read<TState, TResult>(this State<TState> state, Func<TState, TResult> func)
        where TState : class, IStateValue, new()
    {
        await state.Read();
        return func(state.Value);
    }
}