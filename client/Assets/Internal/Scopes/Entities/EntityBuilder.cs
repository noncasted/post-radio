using VContainer.Unity;

namespace Internal
{
    public class EntityBuilder : IEntityBuilder
    {
        public EntityBuilder(
            ServiceCollection services,
            IScopeEntityView view,
            ILifetime lifetime,
            IAssetEnvironment assets)
        {
            Services = services;
            InternalServices = services;
            Scope = view.Scope;
            Lifetime = lifetime;
            Assets = assets;
            View = view;
        }

        public IServiceCollection Services { get; }
        public ServiceCollection InternalServices { get; }
        public LifetimeScope Scope { get; }
        public ILifetime Lifetime { get; }
        public IAssetEnvironment Assets { get; }
        public IScopeEntityView View { get; }
    }
}