using Frontend.Abstract;

namespace Frontend
{
    public interface IViewableList<T> : IEventSource<IReadOnlyLifetime, T>, IReadOnlyList<T>
    {
        IReadOnlyLifetime GetLifetime(T value);
    }
}