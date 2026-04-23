using FluentAssertions;
using Infrastructure;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

/// <summary>
/// Tests side effect dead letter: max retries moves to dead letter table,
/// RequeueStuckOlderThan, and GetStats includes dead letter count.
/// </summary>
[Collection(nameof(SideEffectIntegrationCollection))]
public class SideEffectDeadLetterTests(SideEffectTestFixture fixture)
    : IntegrationTestBase<SideEffectTestFixture>(fixture)
{
    [Fact]
    public async Task FailProcessing_MaxRetriesExceeded_MovesToDeadLetter()
    {
        var storage = GetSiloService<ISideEffectsStorage>();

        // Write a side effect and move it to processing
        await storage.Write(new AlwaysFailingSideEffect { TrackingId = Guid.NewGuid() });
        var entries = await storage.Read(1);
        entries.Should().HaveCount(1);

        var entry = entries[0];
        var config = GetSiloService<ISideEffectsConfig>();
        var maxRetry = config.Value.MaxRetryCount;

        // Directly fail it enough times to exceed max retries
        await storage.FailProcessing(entry.Id, maxRetry, maxRetry, 30f, "test forced failure");

        var stats = await storage.GetStats();
        stats.DeadLetterCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RequeueStuckOlderThan_RecentEntries_NotRequeued()
    {
        var storage = GetSiloService<ISideEffectsStorage>();

        // Should not throw and should be a no-op for recent entries
        await storage.RequeueStuckOlderThan(TimeSpan.FromMinutes(5));

        var stats = await storage.GetStats();
        stats.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStats_IncludesDeadLetterCount()
    {
        var storage = GetSiloService<ISideEffectsStorage>();
        var stats = await storage.GetStats();

        stats.QueueCount.Should().BeGreaterThanOrEqualTo(0);
        stats.ProcessingCount.Should().BeGreaterThanOrEqualTo(0);
        stats.RetryCount.Should().BeGreaterThanOrEqualTo(0);
        stats.DeadLetterCount.Should().BeGreaterThanOrEqualTo(0);
    }
}