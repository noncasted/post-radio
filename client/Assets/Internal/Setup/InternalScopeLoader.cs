using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Internal
{
    public class InternalScopeLoader
    {
        public InternalScopeLoader(IInternalScopeConfig config)
        {
            _config = config;
        }

        private readonly IInternalScopeConfig _config;

        public ILoadedScope Load()
        {
            var profiler = new ProfilingScope("InternalScopeLoader");
            var container = Object.Instantiate(_config.Scope);

            using (LifetimeScope.Enqueue(Register))
                container.Build();

            profiler.Dispose();

            return new InternalLoadedScope(container, new Lifetime());

            void Register(IContainerBuilder containerBuilder)
            {
                var optionsRegistry = _config.AssetsStorage.Options[_config.Platform];
                optionsRegistry.CacheRegistry();
                optionsRegistry.AddOptions(new PlatformOptions(_config.Platform, Application.isMobilePlatform));

                var assets = new AssetEnvironment(_config.AssetsStorage, optionsRegistry);
                var scopeBuilder = new InternalScopeBuilder(assets, containerBuilder);

                scopeBuilder
                    .AddScenes()
                    .AddLogs()
                    .AddScopeLoaders();

                containerBuilder.RegisterInstance(assets)
                    .As<IAssetEnvironment>();
            }
        }
    }
}