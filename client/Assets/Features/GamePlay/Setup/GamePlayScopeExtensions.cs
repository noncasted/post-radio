using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using VContainer;

namespace GamePlay.Setup
{
    public static class GamePlayScopeExtensions
    {
        public static async UniTask<ILoadedScope> ProcessGamePlay(
            this IServiceScopeLoader loader,
            ILoadedScope parent)
        {
            var options = loader.Assets.GetAsset<GamePlayScopeOptions>();
            var scopeLoadResult = await loader.Load(parent, options.Default, Construct);
            await scopeLoadResult.Initialize();

            var loop = scopeLoadResult.Container.Container.Resolve<IGamePlayLoop>();
            await loop.Process(scopeLoadResult.Lifetime);

            return scopeLoadResult;

            UniTask Construct(IScopeBuilder builder)
            {
                builder
                    .AddGamePlayLoop();

                return UniTask.WhenAll(builder.AddScene());
            }
        }

        public static async UniTask<ILoadedScope> LoadGameMock(
            this IServiceScopeLoader loader,
            ILoadedScope parent)
        {
            var options = loader.Assets.GetAsset<GamePlayScopeOptions>();
            var scopeLoadResult = await loader.Load(parent, options.Mock, Construct);
            await scopeLoadResult.Initialize();

            return scopeLoadResult;

            UniTask Construct(IScopeBuilder builder)
            {
                builder
                    .AddGamePlayLoop();

                return UniTask.WhenAll(builder.AddScene());
            }
        }

        private static async UniTask AddScene(this IScopeBuilder builder)
        {
            await builder.FindOrLoadSceneWithServices<GamePlayScene>();
        }
    }
}