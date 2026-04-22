using Infrastructure;
using Infrastructure.Execution;

namespace Cluster.Configs;

public class SideEffectsConfigState
    (AddressableStateUtils utils) : AddressableState<SideEffectsOptions>(utils), ISideEffectsConfig;

public class DurableQueueConfigState(AddressableStateUtils utils)
    : AddressableState<DurableQueueOptions>(utils), IDurableQueueConfig;

public class TaskBalancerConfigState(AddressableStateUtils utils)
    : AddressableState<TaskBalancerOptions>(utils), ITaskBalancerConfig;

public class RuntimePipeConfigState(AddressableStateUtils utils)
    : AddressableState<RuntimePipeOptions>(utils), IRuntimePipeConfig;

public class RuntimeChannelConfigState(AddressableStateUtils utils)
    : AddressableState<RuntimeChannelOptions>(utils), IRuntimeChannelConfig;

public class TransactionConfigState(AddressableStateUtils utils)
    : AddressableState<TransactionOptions>(utils), ITransactionConfig;

public class FrontendConfigState(AddressableStateUtils utils)
    : AddressableState<FrontendOptions>(utils), IFrontendConfig;
