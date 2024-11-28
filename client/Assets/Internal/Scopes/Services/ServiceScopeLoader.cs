using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Internal
{
    public class ServiceScopeLoader : IServiceScopeLoader
    {
        public ServiceScopeLoader(
            IAssetEnvironment assets,
            ISceneLoader sceneLoader,
            ISceneUnloader sceneUnloader)
        {
            _assets = assets;
            _sceneLoader = sceneLoader;
            _sceneUnloader = sceneUnloader;
        }

        private readonly IAssetEnvironment _assets;
        private readonly ISceneLoader _sceneLoader;
        private readonly ISceneUnloader _sceneUnloader;

        public IAssetEnvironment Assets => _assets;

        public async UniTask<ILoadedScope> Load(ILoadedScope parent, ServiceScopeData data, ConstructCallback construct)
        {
            var profiler = new ProfilingScope("ServiceScopeLoader");

            var sceneLoader = new ServiceScopeSceneLoader(_sceneLoader);
            var builder = await CreateBuilder(sceneLoader, parent, data);

            await construct.Invoke(builder);
            BuildContainer(builder, parent.Container);

            var eventLoop = builder.Container.Container.Resolve<IEventLoop>();
            await eventLoop.RunConstruct(builder.Lifetime);

            var disposer = new ServiceScopeDisposer(
                builder.Lifetime,
                eventLoop,
                sceneLoader.Results,
                _sceneUnloader,
                builder.Container);

            var loadResult = new ScopeLoadResult(
                builder.Container,
                builder.Lifetime,
                eventLoop,
                disposer);

            profiler.Dispose();

            return loadResult;
        }

        private async UniTask<ScopeBuilder> CreateBuilder(
            ISceneLoader sceneLoader,
            ILoadedScope parent,
            ServiceScopeData scopeData)
        {
            var servicesScene = await sceneLoader.Load(scopeData.ServicesScene);
            var binder = new ServiceScopeBinder(servicesScene.Scene);
            var scope = Object.Instantiate(scopeData.ScopePrefab);
            binder.MoveToModules(scope.gameObject);
            var lifetime = parent.Lifetime.Child();
            var services = new ServiceCollection();

            return new ScopeBuilder(services, _assets, sceneLoader, binder, scope, lifetime, parent, scopeData.IsMock);
        }

        private void BuildContainer(ScopeBuilder builder, LifetimeScope parent)
        {
            var scope = builder.Container;

            using (LifetimeScope.EnqueueParent(parent))
            {
                using (LifetimeScope.Enqueue(Register))
                {
                    scope.Build();
                }
            }

            builder.ServicesInternal.Resolve(scope.Container);
            return;

            void Register(IContainerBuilder container)
            {
                container.AddEvents();
                container.Register<IViewInjector, ViewInjector>(VContainer.Lifetime.Scoped);
                builder.Events.Register(container);
                builder.ServicesInternal.PassRegistrations(container);
            }
        }
    }
}