namespace Common.Reactive
{
    public interface IViewableList<T> : IEventSource<IReadOnlyLifetime, T>, IReadOnlyList<T>
    {
        IReadOnlyLifetime GetLifetime(T value);
    }
}