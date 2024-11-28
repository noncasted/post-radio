using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace Internal
{
    public interface ILoadedScope
    {
        LifetimeScope Container { get; }
        IReadOnlyLifetime Lifetime { get; }

        UniTask Initialize();
        UniTask Dispose();
    }
}