using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace Internal
{
    public class ScopeLoadResult : ILoadScopeResult
    {
        public ScopeLoadResult(
            LifetimeScope scope,
            ILifetime lifetime,
            IEventLoop eventLoop,
            IScopeDisposer disposer)
        {
            _disposer = disposer;
            Container = scope;
            Lifetime = lifetime;
            EventLoop = eventLoop;
        }

        private readonly IScopeDisposer _disposer;

        public LifetimeScope Container { get; }
        public IReadOnlyLifetime Lifetime { get; }
        public IEventLoop EventLoop { get; }

        public UniTask Initialize()
        {
            return EventLoop.RunLoaded(Lifetime);
        }

        public UniTask Dispose() => _disposer.Dispose();
    }
}