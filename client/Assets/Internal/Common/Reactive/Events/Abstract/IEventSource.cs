using System;

namespace Internal
{
    public interface IEventSourceBase<T> : IDisposable
    {
        void Advise(IReadOnlyLifetime lifetime, T handler);
    }

    public interface IEventSource : IEventSourceBase<Action>
    {
    }

    public interface IEventSource<T> : IEventSourceBase<Action<T>>
    {
    }

    public interface IEventSource<T1, T2> : IEventSourceBase<Action<T1, T2>>
    {
    }

    public interface IEventSource<T1, T2, T3> : IEventSourceBase<Action<T1, T2, T3>>
    {
    }
}