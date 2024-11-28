using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IServiceScopeLoader
    {
        IAssetEnvironment Assets { get; }

        UniTask<ILoadedScope> Load(ILoadedScope parent, ServiceScopeData data, ConstructCallback construct);
    }

    public delegate UniTask ConstructCallback(IScopeBuilder builder);
}