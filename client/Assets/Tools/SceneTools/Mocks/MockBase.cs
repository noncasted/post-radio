using Cysharp.Threading.Tasks;
using Global.Setup;
using Global.UI;
using Internal;
using UnityEngine;
using VContainer;

namespace Tools
{
    [DisallowMultipleComponent]
    public abstract class MockBase : MonoBehaviour
    {
        private ILoadedScope _internalScope;

        public abstract UniTaskVoid Process();

        protected async UniTask<ILoadedScope> BootstrapGlobal()
        {
            var internalConfig = AssetsExtensions.Environment.GetAsset<InternalScopeConfig>();
            var internalScopeLoader = new InternalScopeLoader(internalConfig);
            _internalScope = internalScopeLoader.Load();
            var scopeLoader = _internalScope.Container.Container.Resolve<IServiceScopeLoader>();
            
            var globalScope = await scopeLoader.LoadGlobalMock(_internalScope);
            globalScope.Container.Container.Resolve<ILoadingScreen>().HideGameLoading();
            
            return globalScope;
        }

        private void OnApplicationQuit()
        {
            _internalScope?.Dispose().Forget();
        }
    }
}