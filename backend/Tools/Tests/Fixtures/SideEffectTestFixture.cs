using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Tests.Fixtures;

/// <summary>
/// Extends OrleansTestClusterFixture with side effects pipeline support.
/// Provides pump/drain semantics for testing SE execution.
/// </summary>
public class SideEffectTestFixture : OrleansTestClusterFixture
{
    public SideEffectTestPipeline Pipeline { get; private set; } = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        // Build pipeline from silo's DI container
        var siloServiceProvider = Cluster.Silos[0].ServiceProvider;

        var storage = siloServiceProvider.GetRequiredService<ISideEffectsStorage>();
        var transactions = siloServiceProvider.GetRequiredService<ITransactions>();
        var orleans = siloServiceProvider.GetRequiredService<IOrleans>();
        var config = siloServiceProvider.GetRequiredService<ISideEffectsConfig>();

        Pipeline = new SideEffectTestPipeline(storage, transactions, orleans, config);
    }
}

/// <summary>
/// xUnit collection for tests that need side effects pipeline.
/// </summary>
[CollectionDefinition(nameof(SideEffectIntegrationCollection))]
public class SideEffectIntegrationCollection : ICollectionFixture<SideEffectTestFixture>;