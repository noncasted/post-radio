using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;
using Xunit;

namespace Tests.Fixtures;

/// <summary>
/// Base class for all integration tests.
/// Provides per-test lifecycle management, cleanup, and helper methods.
/// </summary>
public abstract class IntegrationTestBase<TFixture> : IAsyncLifetime
    where TFixture : OrleansTestClusterFixture
{
    protected IntegrationTestBase(TFixture fixture)
    {
        Fixture = fixture;
    }

    protected TFixture Fixture { get; }

    // --- Convenience proxies ---

    protected InProcessTestCluster Cluster => Fixture.Cluster;
    protected IGrainFactory GrainFactory => Fixture.GrainFactory;
    protected DatabaseFixture Database => Fixture.Database;

    /// <summary>
    /// Pipeline proxy — only available if fixture is SideEffectTestFixture.
    /// </summary>
    protected SideEffectTestPipeline? Pipeline =>
        Fixture is SideEffectTestFixture seFixture ? seFixture.Pipeline : null;

    // --- Per-test lifecycle ---

    public virtual async ValueTask InitializeAsync()
    {
        await Database.ResetDatabaseAsync();
    }

    public virtual ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    // --- Transaction helpers ---

    /// <summary>
    /// Run an action inside the project's custom transaction system.
    /// </summary>
    protected async Task RunTransaction(Func<Task> action)
    {
        var transactions = GetSiloService<ITransactions>();
        var result = await transactions.Run(action);

        if (!result.IsSuccess)
            throw new Exception($"Transaction failed: {result}");
    }

    /// <summary>
    /// Get a grain reference from the test cluster.
    /// </summary>
    protected T GetGrain<T>(Guid key) where T : IGrainWithGuidKey
    {
        return GrainFactory.GetGrain<T>(key);
    }

    protected T GetGrain<T>(string key) where T : IGrainWithStringKey
    {
        return GrainFactory.GetGrain<T>(key);
    }

    /// <summary>
    /// Resolve a service from the first silo's DI container.
    /// </summary>
    protected T GetSiloService<T>() where T : notnull
    {
        return Cluster.Silos[0].ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Settle and drain all side effects (requires SideEffectTestFixture).
    /// </summary>
    protected async Task<DrainResult> DrainSideEffectsAsync()
    {
        if (Pipeline == null)
            throw new InvalidOperationException("Pipeline not available. Use SideEffectTestFixture.");
        return await Pipeline.SettleAndDrainAsync();
    }
}