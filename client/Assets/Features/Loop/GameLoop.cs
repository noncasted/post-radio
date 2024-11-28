using Cysharp.Threading.Tasks;
using GamePlay.Setup;
using Global.Cameras;
using Global.GameLoops;
using Global.UI;
using Internal;

namespace Loop
{
    public class GameLoop : IGamePlayLoader
    {
        public GameLoop(
            IReadOnlyLifetime lifetime,
            IServiceScopeLoader scopeLoaderFactory,
            ILoadingScreen loadingScreen,
            IGlobalCamera globalCamera,
            ICurrentCameraProvider currentCameraProvider)
        {
            _lifetime = lifetime;
            _scopeLoaderFactory = scopeLoaderFactory;
            _loadingScreen = loadingScreen;
            _globalCamera = globalCamera;
            _currentCameraProvider = currentCameraProvider;
        }

        private readonly IServiceScopeLoader _scopeLoaderFactory;
        private readonly ILoadingScreen _loadingScreen;
        private readonly IGlobalCamera _globalCamera;
        private readonly ICurrentCameraProvider _currentCameraProvider;
        private readonly IReadOnlyLifetime _lifetime;

        private ILoadedScope _currentScope;
        private ILoadedScope _parent;

        public async UniTask Initialize(ILoadedScope parent)
        {
            _parent = parent;
            await LoadGamePlay();
        }

        private async UniTask LoadGamePlay()
        {
            _globalCamera.Enable();
            _currentCameraProvider.SetCamera(_globalCamera.Camera);

            var unloadTask = UniTask.CompletedTask;

            if (_currentScope != null)
                unloadTask = _currentScope.Dispose();

            var loadResult = await _scopeLoaderFactory.ProcessGamePlay(_parent);

            await unloadTask;
            _currentScope = loadResult;
            _globalCamera.Disable();
        }
    }
}