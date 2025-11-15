using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Common;

public static class ServicesExtensions
{
    public static IServiceCollection AddHostedSingleton<TInterface, TImplementation>(this IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface, IHostedService {
        services.Add<TImplementation>();
        services.AddSingleton<TInterface>(sp => sp.GetRequiredService<TImplementation>());
        services.AddHostedService<TImplementation>(sp => sp.GetRequiredService<TImplementation>());

        return services;
    }
    
    public static IServiceCollection AddHostedSingleton<TImplementation>(this IServiceCollection services)
        where TImplementation : class, IHostedService {
        services.Add<TImplementation>();
        services.AddHostedService<TImplementation>(sp => sp.GetRequiredService<TImplementation>());

        return services;
    }
}