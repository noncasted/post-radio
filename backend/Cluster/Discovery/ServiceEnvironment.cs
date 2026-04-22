using Common.Extensions;
using Microsoft.Extensions.Hosting;

namespace Cluster.Discovery;

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
        builder.Add<IServiceEnvironment>(new ServiceEnvironment
        {
            IsDevelopment = builder.Environment.IsDevelopment(),
            Tag = tag
        });

        return builder;
    }

    public static string ServerUrlToWebSocket(this string url)
    {
        return url.Contains("http://") ? url.Replace("http://", "ws://") : url.Replace("https://", "wss://");
    }
}