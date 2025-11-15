using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Frontend;

public static class OptionsExtensions
{
    public static IHostApplicationBuilder AddOptionsFile(this IHostApplicationBuilder builder, string filePath)
    {
        builder.Configuration.AddJsonFile(filePath);
        return builder;
    }

    public static IHostApplicationBuilder AddOptions<T>(this IHostApplicationBuilder builder, string sectionName)
        where T : class
    {
        var options = builder.Configuration.GetSection(sectionName).Get<T>()!;
        builder.Services.AddSingleton(options);
        return builder;
    }
}