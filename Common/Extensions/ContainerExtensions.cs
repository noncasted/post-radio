using Microsoft.Extensions.DependencyInjection;

namespace Common;

public static class ContainerExtensions
{
    extension(Registration registration)
    {
        public Registration As<T>() where T : class
        {
            registration.Collection.AddSingleton(sp => (T)sp.GetRequiredService(registration.Type));
            return registration;
        }
    }

    public class Registration
    {
        public required Type Type { get; init; }
        public required IServiceCollection Collection { get; init; }
    }

    extension(IServiceCollection builder)
    {
        public Registration Add<TInterface, TImplementation>()
            where TInterface : class
            where TImplementation : class, TInterface
        {
            builder.AddSingleton<TImplementation>();
            builder.AddSingleton<TInterface>(sp => sp.GetRequiredService<TImplementation>());

            return new Registration
            {
                Collection = builder,
                Type = typeof(TImplementation)
            };
        }

        public Registration Add<TImplementation>()
            where TImplementation : class
        {
            builder.AddSingleton<TImplementation>();

            return new Registration
            {
                Collection = builder,
                Type = typeof(TImplementation)
            };
        }
    }
}