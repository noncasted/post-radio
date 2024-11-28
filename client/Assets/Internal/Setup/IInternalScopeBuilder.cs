using VContainer;

namespace Internal
{
    public interface IInternalScopeBuilder
    {
        IAssetEnvironment Assets { get; }
        IContainerBuilder Container { get; }
    }
}