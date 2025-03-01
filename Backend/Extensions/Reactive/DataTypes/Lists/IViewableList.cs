using Extensions.Abstract;

namespace Extensions
{
    public interface IViewableList<T> : IEventSource<IReadOnlyLifetime, T>, IReadOnlyList<T>
    {
        IReadOnlyLifetime GetLifetime(T value);
    }
}