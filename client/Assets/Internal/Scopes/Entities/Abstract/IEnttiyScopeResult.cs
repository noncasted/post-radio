using VContainer.Unity;

namespace Internal
{
    public interface IEntityScopeResult
    {
        LifetimeScope Scope { get; }
        IReadOnlyLifetime Lifetime { get; }
    }
}