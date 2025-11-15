using Microsoft.Extensions.DependencyInjection;

namespace Common;

public static class ContainerExtensions
{
    public class Registration
    {
        public required Type Type { get; init; }
        public required IServiceCollection Collection { get; init; }
    }

    public static Registration Add<TInterface, TImplementation>(this IServiceCollection builder)
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

    public static Registration Add<TImplementation>(this IServiceCollection builder)
        where TImplementation : class
    {
        builder.AddSingleton<TImplementation>();

        return new Registration
        {
            Collection = builder,
            Type = typeof(TImplementation)
        };
    }

    public static Registration As<T>(this Registration registration) where T : class
    {
        registration.Collection.AddSingleton(sp => (T)sp.GetRequiredService(registration.Type));
        return registration;
    }
}