using Cluster.Deploy;
using Microsoft.Extensions.Hosting;

namespace Cluster.Monitoring;

public static class MonitoringExtensions
{
    public static IHostApplicationBuilder AddMonitoring(this IHostApplicationBuilder builder)
    {
        builder.AddLiveState<MatchmakingLiveData>();
        builder.AddLiveState<LiveMatchesData>();
        builder.AddLiveState<ConnectedUsersLiveData>();

        return builder;
    }
}