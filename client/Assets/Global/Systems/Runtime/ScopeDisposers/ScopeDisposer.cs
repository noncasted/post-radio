using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Systems
{
    public class ScopeDisposer : IScopeDisposer
    {
        public ScopeDisposer(ISceneUnloader sceneUnload)
        {
            _sceneUnload = sceneUnload;
        }

        private readonly ISceneUnloader _sceneUnload;

        public async UniTask Unload(IServiceScopeLoadResult scopeLoadResult)
        {
            scopeLoadResult.Lifetime.Terminate();
            await scopeLoadResult.EventLoop.RunDispose();
            await _sceneUnload.Unload(scopeLoadResult.Scenes);
        }
    }
}