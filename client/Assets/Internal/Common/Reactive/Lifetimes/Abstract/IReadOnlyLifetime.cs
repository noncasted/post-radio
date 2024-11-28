using System;
using System.Threading;

namespace Internal
{
    public interface IReadOnlyLifetime
    {
        CancellationToken Token { get; }
        bool IsTerminated { get; }

        void Listen(Action callback);
        void RemoveListener(Action callback);
    }
}