using System;
using VContainer;
using VContainer.Internal;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Internal
{
    public static class ServiceCollectionExtensions
    {
        public static IRegistration Register<T>(
            this IServiceCollection services,
            VContainer.Lifetime lifetime = VContainer.Lifetime.Singleton)
        {
            var type = typeof(T);
            var builder = new RegistrationBuilder(type, lifetime);
            builder.AsSelf();
            var registration = new BaseRegistration(services, builder);
            services.AddBuilder(builder);

            return registration;
        }

        public static IRegistration RegisterInstance<T>(
            this IServiceCollection services,
            T instance,
            VContainer.Lifetime lifetime = VContainer.Lifetime.Singleton)
        {
            if (instance == null)
                throw new NullReferenceException();

            var builder = new InstanceRegistrationBuilder(instance, lifetime).As(typeof(T));
            var registration = new BaseRegistration(services, builder);
            services.AddBuilder(builder);

            return registration;
        }

        public static IRegistration RegisterComponent<T>(
            this IServiceCollection services,
            T component,
            VContainer.Lifetime lifetime = VContainer.Lifetime.Singleton) where T : Object
        {
            if (component == null)
                throw new NullReferenceException();

            var builder = new ComponentRegistrationBuilder(component, lifetime).As(typeof(T));
            builder.AsSelf();
            var registration = new BaseRegistration(services, builder);
            services.AddBuilder(builder);

            return registration;
        }

        public static IRegistration As<T>(this IRegistration registration)
        {
            registration.Registration.As<T>();
            return registration;
        }

        public static IRegistration WithParameter<T>(this IRegistration registration, T parameter)
        {
            registration.Registration.WithParameter(parameter);
            return registration;
        }

        public static IRegistration AsSelf(this IRegistration registration)
        {
            registration.Registration.AsSelf();
            return registration;
        }

        public static IRegistration AsSelfResolvable(this IRegistration registration)
        {
            registration.ServiceCollection.AddSelfResolvable(registration.Registration);
            return registration;
        }
    }
}