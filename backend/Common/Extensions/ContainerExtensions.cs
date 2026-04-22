using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Common.Extensions;

public static class ContainerExtensions
{
    public class Registration
    {
        public required Type Type { get; init; }
        public required IServiceCollection Collection { get; init; }
    }

    extension(IHostApplicationBuilder builder)
    {
        public Registration Add<TInterface, TImplementation>()
            where TInterface : class
            where TImplementation : class, TInterface
        {
            builder.Services.Add<TImplementation>().As<TInterface>();

            return new Registration
            {
                Collection = builder.Services,
                Type = typeof(TImplementation)
            };
        }

        public Registration Add<T>()
            where T : class
        {
            builder.Services.Add<T>();

            return new Registration
            {
                Collection = builder.Services,
                Type = typeof(T)
            };
        }

        public Registration Add<T>(Func<IServiceProvider, T> factory)
            where T : class
        {
            builder.Services.Add(factory);

            return new Registration
            {
                Collection = builder.Services,
                Type = typeof(T)
            };
        }

        public Registration Add<T>(T instance)
            where T : class
        {
            builder.Services.Add(instance);

            return new Registration
            {
                Collection = builder.Services,
                Type = typeof(T)
            };
        }

        public Registration Pass<T>(IServiceProvider services) where T : class
        {
            return builder.Add(services.GetRequiredService<T>());
        }
    }


    extension(IServiceCollection services)
    {
        public Registration Add<TInterface, TImplementation>()
            where TInterface : class
            where TImplementation : class, TInterface
        {
            services.Add<TImplementation>().As<TInterface>();

            return new Registration
            {
                Collection = services,
                Type = typeof(TImplementation)
            };
        }

        public Registration Add<T>()
            where T : class
        {
            services.AddSingleton<T>();

            return new Registration
            {
                Collection = services,
                Type = typeof(T)
            };
        }

        public Registration Add<T>(T instance)
            where T : class
        {
            services.AddSingleton(instance);

            return new Registration
            {
                Collection = services,
                Type = typeof(T)
            };
        }

        public Registration Add<T>(Func<IServiceProvider, T> factory)
            where T : class
        {
            services.AddSingleton(factory);

            return new Registration
            {
                Collection = services,
                Type = typeof(T)
            };
        }

        public Registration Pass<T>(IServiceProvider provider) where T : class
        {
            return services.Add(provider.GetRequiredService<T>());
        }
    }

    extension(Registration registration)
    {
        public Registration As<T>() where T : class
        {
            registration.Collection.AddSingleton(sp => (T)sp.GetRequiredService(registration.Type));
            return registration;
        }
    }
}