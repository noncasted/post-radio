using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace Internal
{
    public static class EntityBuilderExtensions
    {
        public static IEntityRegistration Register<T>(this IEntityBuilder builder)
        {
            return new EntityRegistration(builder, builder.Services.Register<T>());
        }

        public static IEntityRegistration RegisterInstance<T>(
            this IEntityBuilder builder,
            T instance,
            VContainer.Lifetime lifetime = VContainer.Lifetime.Singleton)
        {
            return new EntityRegistration(builder, builder.Services.RegisterInstance(instance, lifetime));
        }

        public static IEntityRegistration RegisterComponent<T>(
            this IEntityBuilder builder,
            T component,
            VContainer.Lifetime lifetime = VContainer.Lifetime.Singleton)
            where T : Object
        {
            return new EntityRegistration(builder, builder.Services.RegisterComponent(component, lifetime));
        }

        public static void Inject<T>(this IEntityBuilder builder, T component)
        {
            builder.Services.Inject(component);
        }

        public static T GetAsset<T>(this IEntityBuilder builder) where T : ScriptableObject
        {
            return builder.Assets.GetAsset<T>();
        }

        public static T GetOptions<T>(this IEntityBuilder builder) where T : class, IOptionsEntry
        {
            return builder.Assets.GetOptions<T>();
        }

        public static IRegistration RegisterAsset<T>(this IEntityBuilder builder) where T : ScriptableObject
        {
            var asset = builder.GetAsset<T>();
            return builder.Services.RegisterInstance(asset);
        }
        
        public static IRegistration RegisterAsset<TSource, TTarget>(this IEntityBuilder builder) 
            where TSource : ScriptableObject, TTarget
        {
            var asset = builder.GetAsset<TSource>();
            return builder.Services.RegisterInstance(asset).As<TTarget>();
        }

        public static IEntityRegistration WithAsset<T>(this IEntityRegistration registration) where T : EnvAsset
        {
            var asset = registration.Builder.GetAsset<T>();
            registration.WithParameter(asset);
            return registration;
        }
        
        public static IEntityRegistration WithAsset<TSource, TTarget>(this IEntityRegistration registration) 
            where TSource : EnvAsset, TTarget
        {
            var asset = registration.Builder.GetAsset<TSource>();
            registration.WithParameter<TTarget>(asset);
            return registration;
        }

        public static T Get<T>(this IEntityScopeResult result)
        {
            return result.Scope.Container.Resolve<T>();
        }
    }
}