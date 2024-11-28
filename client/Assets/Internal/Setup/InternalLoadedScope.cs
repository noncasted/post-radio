using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace Internal
{
    public class InternalLoadedScope : ILoadedScope
    {
        public InternalLoadedScope(LifetimeScope container, ILifetime lifetime)
        {
            _lifetime = lifetime;
            Container = container;
        }

        private readonly ILifetime _lifetime;

        public LifetimeScope Container { get; }
        public IReadOnlyLifetime Lifetime => _lifetime;

        public UniTask Initialize()
        {
            return UniTask.CompletedTask;
        }

        public UniTask Dispose()
        {
            _lifetime.Terminate();
            Container.Dispose();

            return UniTask.CompletedTask;
        }
    }
}