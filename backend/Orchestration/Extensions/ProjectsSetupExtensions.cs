using Benchmarks;
using Cluster;
using Cluster.Configs;
using Cluster.Coordination;
using Cluster.Deploy;
using Cluster.Discovery;
using Cluster.Monitoring;
using Cluster.State;
using Common;
using Common.Extensions;
using Infrastructure;
using Infrastructure.Execution;
using Infrastructure.Startup;
using Infrastructure.State;
using Meta.Audio;
using Meta.Images;
using Meta.Online;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Orchestration;

public static class ProjectsSetupExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder SetupCoordinator()
        {
            builder
                .AddServiceDefaults()
                .AddOrleansClient();

            builder
                .AddBase(ServiceTag.Coordinator);

            builder.Services.AddHealthChecks()
                   .AddCheck<CoordinatorReadyHealthCheck>("coordinator-deploy", tags: ["ready"]);

            return builder;
        }

        public IHostApplicationBuilder SetupMetaGateway()
        {
            builder
                .AddServiceDefaults()
                .AddOrleansClient();

            builder
                .AddBase(ServiceTag.Meta)
                .ConfigureCors();

            builder.Services.AddOpenApi();

            return builder;
        }

        public IHostApplicationBuilder SetupSilo()
        {
            builder
                .AddServiceDefaults()
                .ConfigureSilo();

            builder
                .AddBase(ServiceTag.Silo);

            builder.Add<SideEffectsWorker>()
                   .As<IHostedService>();

            return builder;
        }

        public IHostApplicationBuilder SetupConsole()
        {
            builder
                .AddServiceDefaults()
                .AddOrleansClient();

            builder
                .AddBase(ServiceTag.Console)
                .AddBlazorComponents();

            builder.Services.AddHttpClient("meta", c => c.BaseAddress = new Uri("http://meta"));

            var testsAssembly = typeof(IClusterTest).Assembly;

            foreach (var type in testsAssembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                if (!typeof(IClusterTest).IsAssignableFrom(type))
                    continue;

                builder.Services.AddSingleton(type);
                builder.Services.AddSingleton(typeof(IClusterTest), sp => sp.GetRequiredService(type));
            }

            return builder;
        }

        private IHostApplicationBuilder AddBase(ServiceTag serviceTag)
        {
            if (builder is WebApplicationBuilder webBuilder)
                webBuilder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = true);

            builder.Services.AddHostedService<ClusterParticipantStartup>();
            builder.Services.AddHostedService<DeployHealthChecker>();

            builder.Add<ClusterParticipantContext>()
                   .As<IClusterParticipantContext>();

            builder.Add<DeployContext>()
                   .As<IDeployContext>();

            builder
                .AddEnvironment(serviceTag)
                .AddServiceLoop()
                .AddMessaging()
                .AddOrleansUtils()
                .AddServiceDiscovery()
                .AddTaskScheduling()
                .AddDeployCleanup()
                .AddClusterFeatures()
                .AddConfigs()
                .AddSideEffects()
                .AddTests()
                .AddStates()
                .AddMonitoring()
                .AddHeapDiagnostics()
                .AddMediaStorage()
                .AddAudioServices()
                .AddImagesServices()
                .AddOnlineServices();

            builder.Add<DbSource>()
                   .As<IDbSource>();

            builder.Add<StateStorageReader>()
                   .As<IStateStorageReader>();

            builder.Services.AddHostedService<MetricsSnapshotService>();

            return builder;
        }

        private IHostApplicationBuilder AddStates()
        {
            var states = new List<GrainStateInfo>();
            GeneratedStatesRegistration.AddAllStates(states);
            var registry = new GrainStatesRegistry(states);
            builder.Add(registry).As<IGrainStatesRegistry>();
            return builder;
        }

        private IHostApplicationBuilder AddSideEffects()
        {
            builder.Add<SideEffectsStorage>()
                   .As<ISideEffectsStorage>();

            builder.Services.Add<SideEffectsMonitorService>()
                   .As<ILocalSetupCompleted>();

            return builder;
        }

        private IHostApplicationBuilder AddBlazorComponents()
        {
            builder.Services
                   .AddRazorComponents()
                   .AddInteractiveServerComponents()
                   .AddHubOptions(options => {
                       options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
                   });

            return builder;
        }

        private IHostApplicationBuilder AddTests()
        {
            builder.Add<BenchmarkRunner>();
            builder.Add<BenchmarkStorage>();
            builder.Add<ClusterTestUtils>();

            var testsAssembly = typeof(IClusterTest).Assembly;

            foreach (var type in testsAssembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                if (!IsTestNodeType(type))
                    continue;

                builder.Services.AddSingleton(type);
                builder.Services.AddSingleton(typeof(ICoordinatorSetupCompleted), sp => sp.GetRequiredService(type));
            }

            builder.Add<StateMigrationTest.MigrationTestStep_V0>()
                   .As<IStateMigrationStep>();

            builder.Add<StateMigrationTest.MigrationTestStep_V1>()
                   .As<IStateMigrationStep>();

            return builder;
        }

        private static bool IsTestNodeType(Type type)
        {
            var current = type.BaseType;

            while (current != null)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(BenchmarkNode<>))
                    return true;

                current = current.BaseType;
            }

            return false;
        }
    }
}
