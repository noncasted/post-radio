using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using GamePlay.Setup;
using Global.GameServices;
using Internal;

namespace Tools
{
    public class GameMock : MockBase
    {
        public override async UniTaskVoid Process()
        {
            var globalScope = await BootstrapGlobal();

            var scopeLoaderFactory = globalScope.Get<IServiceScopeLoader>();
            var gameScope = await scopeLoaderFactory.LoadGameMock(globalScope);

            var localUserList = globalScope.Get<ILocalUserList>();

            await UniTask.WaitUntil(() => localUserList.Count != 0);
            
            var loop = gameScope.Get<IGamePlayLoop>();
            loop.Process(gameScope.Lifetime).Forget();
        }
    }
}