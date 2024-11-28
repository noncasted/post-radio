using VContainer;

namespace Internal
{
    public class InternalScopeBuilder : IInternalScopeBuilder
    {
        public InternalScopeBuilder(IAssetEnvironment assets, IContainerBuilder container)
        {
            Assets = assets;
            Container = container;
        }

        public IAssetEnvironment Assets { get; }
        public IContainerBuilder Container { get; }
    }
}