using VContainer.Unity;

namespace Internal
{
    public interface IScopeEntityView
    {
        public LifetimeScope Scope { get; }
        
        void CreateViews(IEntityBuilder builder);
    }
}