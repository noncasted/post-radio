using Cysharp.Threading.Tasks;
using Global.GameLoops;
using Internal;
using UnityEngine;
using VContainer;

namespace Global.Setup
{
    [DisallowMultipleComponent]
    public class GameSetup : MonoBehaviour
    {
        [SerializeField] private InternalScopeConfig _internal;
        [SerializeField] private SetupLoadingScreen _loading;
        
        private ILoadedScope _internalScope;

        private void Awake()
        {
            Setup().Forget();
        }

        private async UniTask Setup()
        {
            var profiler = new ProfilingScope("GameSetup");
            var internalScopeLoader = new InternalScopeLoader(_internal);

            _internalScope = internalScopeLoader.Load();

            var scopeLoader = _internalScope.Container.Container.Resolve<IServiceScopeLoader>();
            var globalScope = await scopeLoader.LoadGlobal(_internalScope);

            var gamePlayLoader = globalScope.Get<IGamePlayLoader>();
            await gamePlayLoader.Initialize(globalScope);

            _loading.Dispose();
            profiler.Dispose();
        }
        
        private void OnDestroy()
        {
            _internalScope.Dispose().Forget();
        }
    }
}