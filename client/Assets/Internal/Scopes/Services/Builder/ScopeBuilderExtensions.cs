using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    public static class ScopeBuilderExtensions
    {
        public static IServiceRegistration Register<T>(this IScopeBuilder builder)
        {
            return new ScopeBuilderRegistration(builder, builder.Services.Register<T>());
        }

        public static IRegistration RegisterInstance<T>(
            this IScopeBuilder builder,
            T instance,
            VContainer.Lifetime lifetime = VContainer.Lifetime.Singleton)
        {
            return new ScopeBuilderRegistration(builder, builder.Services.RegisterInstance(instance, lifetime));
        }

        public static IRegistration RegisterComponent<T>(
            this IScopeBuilder builder,
            T component, 
            VContainer.Lifetime lifetime = VContainer.Lifetime.Singleton) where T : Object
        {
            return new ScopeBuilderRegistration(builder, builder.Services.RegisterComponent(component, lifetime));
        }

        public static void Inject<T>(this IScopeBuilder builder, T component)
        {
            builder.Services.Inject(component);
        }

        public static T GetAsset<T>(this IScopeBuilder builder) where T : ScriptableObject
        {
            return builder.Assets.GetAsset<T>();
        }

        public static T GetOptions<T>(this IScopeBuilder builder) where T : class, IOptionsEntry
        {
            return builder.Assets.GetOptions<T>();
        }

        public static IRegistration RegisterAsset<T>(this IScopeBuilder builder) where T : ScriptableObject
        {
            var asset = builder.GetAsset<T>();
            return builder.Services.RegisterInstance(asset);
        }

        public static UniTask<TComponent> FindOrLoadScene<TScene, TComponent>(this IScopeBuilder builder)
            where TScene : SceneData
            where TComponent : MonoBehaviour
        {
            var scene = builder.GetAsset<TScene>();
            return builder.FindOrLoadScene<TComponent>(scene);
        }

        public static async UniTask FindOrLoadSceneWithServices<TScene>(this IScopeBuilder builder)
            where TScene : SceneData
        {
            var scene = builder.GetAsset<TScene>();
            var services = await builder.FindOrLoadScene<SceneServicesFactory>(scene);
            services.Create(builder);
        }

        public static IServiceRegistration WithAsset<T>(this IServiceRegistration registration) where T : EnvAsset
        {
            var asset = registration.Builder.GetAsset<T>();
            registration.WithParameter(asset);
            return registration;
        }
        
        public static IRegistration WithScopeLifetime(this IServiceRegistration registration)
        {
            registration.Registration.WithParameter<IReadOnlyLifetime>(registration.Builder.Lifetime);
            return registration;
        }

        public static IServiceRegistration WithScriptableRegistry<T1, T2>(this IServiceRegistration registration)
            where T2 : EnvAsset
            where T1 : ScriptableRegistry<T2>
        {
            var asset = registration.Builder.GetAsset<T1>();
            registration.WithParameter(asset.Objects);
            return registration;
        }

        public static IServiceRegistration RegisterScriptableRegistry<T1, T2>(this IScopeBuilder builder)
            where T1 : ScriptableRegistry<T2>
            where T2 : EnvAsset
        {
            var registry = builder.GetAsset<T1>();
            var registration = builder.RegisterInstance(registry);
            registration.As<IScriptableRegistry<T2>>();
            return new ScopeBuilderRegistration(builder, registration);
        }

        public static IScopeBuilder AddFromFactory<T>(this IScopeBuilder builder) where T : ServiceFactoryBase
        {
            return builder.GetAsset<T>().Process(builder);
        }

        public static UniTask<IScopeBuilder> AddFromAsyncFactory<T>(this IScopeBuilder builder)
            where T : ServiceFactoryBaseAsync
        {
            return builder.GetAsset<T>().Process(builder);
        }

        public static T Instantiate<T>(this IScopeBuilder builder, T prefab) where T : MonoBehaviour
        {
            var instance = Object.Instantiate(prefab);
            instance.name = prefab.name;
            builder.Binder.MoveToModules(instance);

            return instance;
        }

        public static T Instantiate<T>(this IScopeBuilder builder, T prefab, Vector3 position) where T : MonoBehaviour
        {
            var instance = Object.Instantiate(prefab, position, Quaternion.identity);
            instance.name = prefab.name;
            builder.Binder.MoveToModules(instance);

            return instance;
        }
    }
}