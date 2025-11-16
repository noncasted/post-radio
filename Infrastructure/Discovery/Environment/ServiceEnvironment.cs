using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Discovery;

public interface IServiceEnvironment
{
    Guid ServiceId { get; }
    bool IsDevelopment { get; }
    ServiceTag Tag { get; }
}

public class ServiceEnvironment : IServiceEnvironment
{
    public Guid ServiceId { get; } = Guid.NewGuid();
    public required bool IsDevelopment { get; init; }
    public required ServiceTag Tag { get; init; }
}

public static class EnvironmentExtensions
{
    public static IHostApplicationBuilder AddEnvironment(this IHostApplicationBuilder builder, ServiceTag tag)
    {
        if (builder.Environment.IsDevelopment() == true)
            builder.Services.AddSingleton<IServiceEnvironment>(
                new ServiceEnvironment
                {
                    IsDevelopment = true,
                    Tag = tag
                }
            );
        else
            builder.Services.AddSingleton<IServiceEnvironment>(
                new ServiceEnvironment
                {
                    IsDevelopment = false,
                    Tag = tag
                }
            );

        return builder;
    }

    public static string ServerUrlToWebSocket(this string url)
    {
        return url.Contains("http://") == true ? url.Replace("http://", "ws://") : url.Replace("https://", "wss://");
    }
}