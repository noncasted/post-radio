using Cluster.Configs;
using Cluster.Discovery;
using Cluster.State;
using Common;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Infrastructure.Execution;
using Infrastructure.Startup;
using Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Orleans.TestingHost;
using Tests.Grains;
using Xunit;
using TaskScheduler = Infrastructure.Execution.TaskScheduler;

namespace Tests.Fixtures;

/// <summary>
/// Base fixture for Orleans integration tests.
/// Spins up an in-process TestCluster with a real PostgreSQL database.
/// </summary>
public class OrleansTestClusterFixture : IAsyncLifetime
{
    private InProcessTestCluster _cluster = null!;

    public InProcessTestCluster Cluster => _cluster;
    public IGrainFactory GrainFactory => _cluster.Client;
    public DatabaseFixture Database { get; } = new();

    protected virtual int SiloCount => 1;

    /// <summary>
    /// Override to register additional silo-level services (e.g. game dependencies).
    /// </summary>
    protected virtual void ConfigureSiloServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// Override to configure ISiloBuilder (e.g. grain extensions).
    /// </summary>
    protected virtual void ConfigureSilo(ISiloBuilder siloBuilder)
    {
    }

    public virtual async ValueTask InitializeAsync()
    {
        await Database.InitializeAsync();

        var builder = new InProcessTestClusterBuilder((short)SiloCount);

        var dataSource = Database.DataSource;

        builder.ConfigureSilo((options, siloBuilder) => {
            siloBuilder.AddMemoryGrainStorage("Default");

            siloBuilder.AddGrainExtension<IGrainTransactionHandler, GrainTransactionHandler>();

            siloBuilder.ConfigureServices(services => {
                // Database source
                var dbSource = Substitute.For<IDbSource>();
                dbSource.Value.Returns(dataSource);
                services.AddSingleton(dbSource);

                // State registry — register all grain states
                services.AddSingleton<IGrainStatesRegistry>(BuildStatesRegistry());

                // Core Orleans utilities
                services.AddSingleton<IStateSerializer, StateSerializer>();
                services.AddSingleton<IStateMigrations, StateMigrations>();
                services.AddSingleton<IStateStorage, StateStorage>();
                services.AddSingleton<ITransactions, Transactions>();
                services.AddSingleton<IOrleans, OrleansUtils>();
                services.AddSingleton<ISideEffectsStorage, SideEffectsStorage>();

                // State factory for grain [State] attribute injection
                services.AddSingleton<IStateFactory, StateFactory>();
                services.AddSingleton<IAttributeToFactoryMapper<StateAttribute>, StateAttributeMapper>();

                // Migration steps (V0→V1 chain targeting MigrationTestState_1)
                services.AddSingleton<IStateMigrationStep, MigrationTestStep_V0>();
                services.AddSingleton<IStateMigrationStep, MigrationTestStep_V1>();

                // Migration steps (V0→V1→V2 chain targeting MigrationTestState_2)
                services.AddSingleton<IStateMigrationStep, MigrationV2TestStep_V0>();
                services.AddSingleton<IStateMigrationStep, MigrationV2TestStep_V1>();
                services.AddSingleton<IStateMigrationStep, MigrationTestStep_V2>();

                // StateCollection utilities for tests
                services.AddSingleton(typeof(StateCollectionUtils<,>));

                // Messaging
                services.AddSingleton<IMessaging, Infrastructure.Messaging>();
                services.AddSingleton<IDurableQueueClient, DurableQueueClient>();
                services.AddSingleton<IRuntimePipeClient, RuntimePipeClient>();
                services.AddSingleton<IRuntimeChannelClient, RuntimeChannelClient>();

                // Service environment
                services.AddSingleton<IServiceEnvironment>(new ServiceEnvironment
                {
                    IsDevelopment = true,
                    Tag = ServiceTag.Silo
                });

                // Service loop — mock as started
                var loopObserver = Substitute.For<IServiceLoopObserver>();
                loopObserver.IsOrleansStarted.Returns(new ViewableProperty<bool>(true));
                services.AddSingleton(loopObserver);

                // Cluster participant context — mock as initialized
                var participantContext = Substitute.For<IClusterParticipantContext>();
                participantContext.IsInitialized.Returns(new ViewableProperty<bool>(true));
                services.AddSingleton(participantContext);

                // Cluster flags — all enabled
                var clusterFlags = Substitute.For<IClusterFlags>();
                clusterFlags.MatchmakingEnabled.Returns(true);
                clusterFlags.SideEffectsEnabled.Returns(true);
                clusterFlags.SnapshotDiffGuardEnabled.Returns(true);
                services.AddSingleton(clusterFlags);

                // Configs — all with default values via TestAddressableState
                RegisterTestConfigs(services);

                // Task scheduling
                services.AddSingleton<ITaskScheduler, TaskScheduler>();
                services.AddSingleton<ITaskQueue, TaskQueue>();
                services.AddSingleton<ITaskBalancer, TaskBalancer>();

                // Custom silo services from derived fixtures
                ConfigureSiloServices(services);
            });

            ConfigureSilo(siloBuilder);
        });

        _cluster = builder.Build();
        await _cluster.DeployAsync();

        // Start messaging (resubscribe loops) — in production this is done by ServiceLoop
        var messaging = _cluster.Silos[0].ServiceProvider.GetRequiredService<IMessaging>();
        var testLifetime = new Lifetime();
        await messaging.Start(testLifetime);
        _messagingLifetime = testLifetime;
    }

    private Lifetime? _messagingLifetime;

    private static void RegisterTestConfigs(IServiceCollection services)
    {
        // Infrastructure configs — loaded from Orchestration/Coordinator JSON files
        RegisterConfig<ISideEffectsConfig, SideEffectsOptions>(services, "config.sideEffects");

        RegisterConfig<ITransactionConfig, TransactionOptions>(services, "config.transaction",
            o => {
                o.LockWaitSeconds = 2f;
                o.StuckGraceSeconds = 5f;
            });
        RegisterConfig<IDurableQueueConfig, DurableQueueOptions>(services, "config.durableQueue");
        RegisterConfig<ITaskBalancerConfig, TaskBalancerOptions>(services, "config.taskBalancer");

        // No JSON files for these — use defaults
        RegisterConfig<IRuntimePipeConfig, RuntimePipeOptions>(services);
        RegisterConfig<IRuntimeChannelConfig, RuntimeChannelOptions>(services);

        // Cluster features
        var features = new TestAddressableState<ClusterFeaturesState>();
        var clusterFeatures = Substitute.For<IClusterFeatures>();
        clusterFeatures.Value.Returns(features.Value);
        clusterFeatures.IsInitialized.Returns(true);
        clusterFeatures.MatchmakingEnabled.Returns(true);
        clusterFeatures.SideEffectsEnabled.Returns(true);
        clusterFeatures.SnapshotDiffGuardEnabled.Returns(true);
        services.AddSingleton(clusterFeatures);
    }

    private static void RegisterConfig<TInterface, TOptions>(
        IServiceCollection services,
        string? jsonName = null,
        Action<TOptions>? configure = null)
        where TInterface : class, IAddressableState<TOptions>
        where TOptions : class, new()
    {
        var value = jsonName != null ? ConfigLoader.Load<TOptions>(jsonName) : new TOptions();
        configure?.Invoke(value);
        var state = new TestAddressableState<TOptions>(value);
        services.AddSingleton<TInterface>(CreateConfigMock<TInterface, TOptions>(state));
    }

    private static TInterface CreateConfigMock<TInterface, TOptions>(TestAddressableState<TOptions> state)
        where TInterface : class, IAddressableState<TOptions>
        where TOptions : class, new()
    {
        var mock = Substitute.For<TInterface>();
        mock.Value.Returns(state.Value);
        mock.IsInitialized.Returns(true);
        return mock;
    }

    private static GrainStatesRegistry BuildStatesRegistry()
    {
        var states = new List<GrainStateInfo>();
        GeneratedStatesRegistration.AddAllStates(states);
        return new GrainStatesRegistry(states);
    }

    public virtual async ValueTask DisposeAsync()
    {
        _messagingLifetime?.Terminate();
        await _cluster.StopAllSilosAsync();
        await Database.DisposeAsync();
    }
}

/// <summary>
/// xUnit collection for Orleans integration tests.
/// Tests in this collection share a single TestCluster.
/// </summary>
[CollectionDefinition(nameof(OrleansIntegrationCollection))]
public class OrleansIntegrationCollection : ICollectionFixture<OrleansTestClusterFixture>;
