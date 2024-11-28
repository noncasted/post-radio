using VContainer.Unity;

namespace Internal
{
    public class ScopeBuilder : IScopeBuilder
    {
        public ScopeBuilder(
            ServiceCollection services,
            IAssetEnvironment assets,
            ISceneLoader sceneLoader,
            IServiceScopeBinder binder,
            LifetimeScope container,
            ILifetime lifetime,
            ILoadedScope parent,
            bool isMock)
        {
            Services = services;
            ServicesInternal = services;
            Assets = assets;
            SceneLoader = sceneLoader;
            Binder = binder;
            Container = container;
            Lifetime = lifetime;
            IsMock = isMock;
            Parent = parent;
            Events = new ScopeEventListeners();
        }

        public IServiceCollection Services { get; }
        public IAssetEnvironment Assets { get; }
        public ISceneLoader SceneLoader { get; }
        public IServiceScopeBinder Binder { get; }
        public IScopeEventListeners Events { get; }
        public ILoadedScope Parent { get; }
        public LifetimeScope Container { get; }
        public ILifetime Lifetime { get; }
        public bool IsMock { get; }
        
        public ServiceCollection ServicesInternal { get; }
    }
}