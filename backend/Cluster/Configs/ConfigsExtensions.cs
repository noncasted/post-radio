using Common.Extensions;
using Infrastructure;
using Infrastructure.Execution;
using Microsoft.Extensions.Hosting;

namespace Cluster.Configs;

public static class ConfigsExtensions
{
    public static IHostApplicationBuilder AddConfigs(this IHostApplicationBuilder builder)
    {
        builder.AddAddressableState<SideEffectsConfigState>()
               .As<ISideEffectsConfig>();

        builder.AddAddressableState<DurableQueueConfigState>()
               .As<IDurableQueueConfig>();

        builder.AddAddressableState<TaskBalancerConfigState>()
               .As<ITaskBalancerConfig>();

        builder.AddAddressableState<RuntimePipeConfigState>()
               .As<IRuntimePipeConfig>();

        builder.AddAddressableState<RuntimeChannelConfigState>()
               .As<IRuntimeChannelConfig>();

        builder.AddAddressableState<TransactionConfigState>()
               .As<ITransactionConfig>();

        builder.AddAddressableState<FrontendConfigState>()
               .As<IFrontendConfig>();

        return builder;
    }
}
