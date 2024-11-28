using VContainer.Unity;

namespace Internal
{
    public interface IEntityBuilder
    {
        IServiceCollection Services { get; }
        LifetimeScope Scope { get; }
        ILifetime Lifetime { get; }
        IAssetEnvironment Assets { get; }
        IScopeEntityView View { get; }
    }
}