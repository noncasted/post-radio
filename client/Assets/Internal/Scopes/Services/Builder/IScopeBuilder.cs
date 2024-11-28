using VContainer.Unity;

namespace Internal
{
    public interface IScopeBuilder
    {
        IServiceCollection Services { get; }
        IAssetEnvironment Assets { get; }
        ISceneLoader SceneLoader { get; }
        IServiceScopeBinder Binder { get; }
        IScopeEventListeners Events { get; }
        ILoadedScope Parent { get; }
        LifetimeScope Container { get; }
        ILifetime Lifetime { get; }
        bool IsMock { get; }
    }
}