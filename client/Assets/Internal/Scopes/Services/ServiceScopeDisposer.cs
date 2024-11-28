using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace Internal
{
    public class ServiceScopeDisposer : IScopeDisposer
    {
        public ServiceScopeDisposer(
            ILifetime lifetime,
            IEventLoop loop,
            IReadOnlyList<ISceneLoadResult> scenes,
            ISceneUnloader sceneUnloader,
            LifetimeScope container)
        {
            _lifetime = lifetime;
            _loop = loop;
            _scenes = scenes;
            _sceneUnloader = sceneUnloader;
            _container = container;
        }

        private readonly ILifetime _lifetime;
        private readonly IEventLoop _loop;
        private readonly IReadOnlyList<ISceneLoadResult> _scenes;
        private readonly ISceneUnloader _sceneUnloader;
        private readonly LifetimeScope _container;

        public async UniTask Dispose()
        {
            await _loop.RunDispose();
            _lifetime.Terminate();
            await _sceneUnloader.Unload(_scenes);
            _container.Dispose();
        }
    }
}