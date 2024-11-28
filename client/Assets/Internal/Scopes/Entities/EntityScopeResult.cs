using VContainer.Unity;

namespace Internal
{
    public class EntityScopeResult : IEntityScopeResult
    {
        public EntityScopeResult(LifetimeScope scope, IReadOnlyLifetime lifetime)
        {
            Scope = scope;
            Lifetime = lifetime;
        }

        public LifetimeScope Scope { get; }
        public IReadOnlyLifetime Lifetime { get; }
    }
}