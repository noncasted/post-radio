using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Common;

public static class ServicesExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddHostedSingleton<TInterface, TImplementation>()
            where TInterface : class
            where TImplementation : class, TInterface, IHostedService
        {
            services.Add<TImplementation>();
            services.AddSingleton<TInterface>(sp => sp.GetRequiredService<TImplementation>());
            services.AddHostedService<TImplementation>(sp => sp.GetRequiredService<TImplementation>());

            return services;
        }

        public IServiceCollection AddHostedSingleton<TImplementation>()
            where TImplementation : class, IHostedService
        {
            services.Add<TImplementation>();
            services.AddHostedService<TImplementation>(sp => sp.GetRequiredService<TImplementation>());

            return services;
        }
    }
}