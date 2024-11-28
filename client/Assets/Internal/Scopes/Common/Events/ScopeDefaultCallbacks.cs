using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IScopeBaseSetup
    {
        void OnBaseSetup(IReadOnlyLifetime lifetime);
    }

    public interface IScopeBaseSetupAsync
    {
        UniTask OnBaseSetupAsync(IReadOnlyLifetime lifetime);
    }

    public interface IScopeSetup
    {
        void OnSetup(IReadOnlyLifetime lifetime);
    }

    public interface IScopeSetupAsync
    {
        UniTask OnSetupAsync(IReadOnlyLifetime lifetime);
    }

    public interface IScopeSetupCompletion
    {
        void OnSetupCompletion(IReadOnlyLifetime lifetime);
    }

    public interface IScopeSetupCompletionAsync
    {
        UniTask OnSetupCompletionAsync(IReadOnlyLifetime lifetime);
    }
    
    public interface IScopeLoaded
    {
        void OnLoaded(IReadOnlyLifetime lifetime);
    }

    public interface IScopeLoadedAsync
    {
        UniTask OnLoadedAsync(IReadOnlyLifetime lifetime);
    }
    
    public interface IScopeDispose
    {
        void OnDispose();
    }

    public interface IScopeDisposeAsync
    {
        UniTask OnDisposeAsync();
    }
}