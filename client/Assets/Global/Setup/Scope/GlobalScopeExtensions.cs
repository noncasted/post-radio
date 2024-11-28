using Cysharp.Threading.Tasks;
using Global.Audio;
using Global.Backend;
using Global.Cameras;
using Global.GameLoops;
using Global.GameServices;
using Global.Inputs;
using Global.Publisher;
using Global.Systems;
using Global.UI;
using Internal;

namespace Global.Setup
{
    public static class GlobalScopeExtensions
    {
        public static async UniTask<ILoadedScope> LoadGlobal(this IServiceScopeLoader loader, ILoadedScope parent)
        {
            var options = loader.Assets.GetAsset<GlobalScopeOptions>();
            var scope = await loader.Load(parent, options.Default, Construct);
            await scope.Initialize();

            return scope;

            UniTask Construct(IScopeBuilder builder)
            {
                builder
                    .AddAudio()
                    .AddCamera()
                    .AddInput()
                    .AddLoop()
                    .AddGameServices()
                    .AddBackend()
                    .AddSystemUtils();

                return UniTask.WhenAll(
                    builder.AddPublisher(),
                    builder.AddUI());
            }
        }
        
        public static async UniTask<ILoadedScope> LoadGlobalMock(this IServiceScopeLoader loader, ILoadedScope parent)
        {
            var options = loader.Assets.GetAsset<GlobalScopeOptions>();
            var scope = await loader.Load(parent, options.Default, Construct);
            await scope.Initialize();

            return scope;

            UniTask Construct(IScopeBuilder builder)
            {
                builder
                    .AddAudio()
                    .AddCamera()
                    .AddInput()
                    .AddBackend()
                    .AddGameServices()
                    .AddSystemUtils();

                return UniTask.WhenAll(
                    builder.AddPublisher(),
                    builder.AddUI());
            }
        }
    }
}