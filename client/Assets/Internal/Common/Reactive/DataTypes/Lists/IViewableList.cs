using System.Collections.Generic;

namespace Internal
{
    public interface IViewableList<T> : IEventSource<IReadOnlyLifetime, T>, IReadOnlyList<T>
    {
        IReadOnlyLifetime GetLifetime(T value);
    }
}