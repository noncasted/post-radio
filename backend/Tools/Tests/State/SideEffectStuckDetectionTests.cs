using FluentAssertions;
using Infrastructure;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

/// <summary>
/// Tests RequeueStuckOlderThan time-based detection, retry count progression,
/// stats accuracy, and mixed-outcome batch processing.
/// </summary>
[Collection(nameof(SideEffectIntegrationCollection))]
public class SideEffectStuckDetectionTests(SideEffectTestFixture fixture)
    : IntegrationTestBase<SideEffectTestFixture>(fixture)
{
    // --- RequeueStuckOlderThan ---

    [Fact]
    public async Task RequeueStuckOlderThan_OldEntries_RequeuesAndExecutes()
    {
        var targetId = Guid.NewGuid();
        var storage = GetSiloService<ISideEffectsStorage>();

        // Write and read to move entry into processing
        await storage.Write(new TestSideEffect { TargetGrainId = targetId });
        var entries = await storage.Read(1);
        entries.Should().HaveCount(1);

        // Queue should now be empty — entry is in processing
        var peek = await storage.Read(1);
        peek.Should().BeEmpty();

        // Backdating processing_started_at makes the entry appear stuck
        await SetProcessingTimestamp(TimeSpan.FromSeconds(10));

        // Requeue entries older than 1 second — should move processing → queue
        await storage.RequeueStuckOlderThan(TimeSpan.FromSeconds(1));

        // Entry is now back in queue — drain should execute it
        var result = await DrainSideEffectsAsync();
        result.AssertDrainedWithWork();

        var targetGrain = GetGrain<ITxTestGrain>(targetId);
        var value = await targetGrain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task RequeueStuckOlderThan_RecentEntries_NotRequeued()
    {
        var targetId = Guid.NewGuid();
        var storage = GetSiloService<ISideEffectsStorage>();

        // Write and read to move entry into processing
        await storage.Write(new TestSideEffect { TargetGrainId = targetId });
        var entries = await storage.Read(1);
        entries.Should().HaveCount(1);

        // Do NOT backdate — entry was just moved in, well within 5-minute threshold
        await storage.RequeueStuckOlderThan(TimeSpan.FromMinutes(5));

        // Entry should still be in processing, queue remains empty
        var peek = await storage.Read(1);
        peek.Should().BeEmpty("recent processing entry must not be requeued");

        var stats = await storage.GetStats();
        stats.ProcessingCount.Should().Be(1, "entry should remain in processing");
        stats.QueueCount.Should().Be(0);
    }

    // --- Stats accuracy ---

    [Fact]
    public async Task GetStats_AfterWriteAndRead_ReflectsCorrectCounts()
    {
        var storage = GetSiloService<ISideEffectsStorage>();

        // Write 3 side effects
        await storage.Write(new TestSideEffect { TargetGrainId = Guid.NewGuid() });
        await storage.Write(new TestSideEffect { TargetGrainId = Guid.NewGuid() });
        await storage.Write(new TestSideEffect { TargetGrainId = Guid.NewGuid() });

        var statsAfterWrite = await storage.GetStats();
        statsAfterWrite.QueueCount.Should().Be(3);
        statsAfterWrite.ProcessingCount.Should().Be(0);

        // Read 2 — moves 2 entries to processing
        var read = await storage.Read(2);
        read.Should().HaveCount(2);

        var statsAfterRead = await storage.GetStats();
        statsAfterRead.QueueCount.Should().Be(1, "one entry should remain in queue");
        statsAfterRead.ProcessingCount.Should().Be(2, "two entries moved to processing");
    }

    [Fact]
    public async Task GetStats_AfterFailProcessing_RetryCountIncremented()
    {
        var storage = GetSiloService<ISideEffectsStorage>();

        await storage.Write(new AlwaysFailingSideEffect { TrackingId = Guid.NewGuid() });

        // Pump once — reads, fails, moves to retry queue
        var pump = await Pipeline!.PumpOnceAsync();
        pump.AllSucceeded.Should().BeFalse();

        var stats = await storage.GetStats();
        stats.QueueCount.Should().Be(0);
        stats.ProcessingCount.Should().Be(0);
        stats.RetryCount.Should().Be(1, "failed entry should be in retry queue");
    }

    // --- Retry count progression ---

    [Fact]
    public async Task RetryCount_IncrementsOnEachFailedAttempt()
    {
        FailingTestSideEffect.ResetAttempts();
        var targetId = Guid.NewGuid();
        var storage = GetSiloService<ISideEffectsStorage>();

        // SE that fails 3 times, succeeds on 4th
        await storage.Write(new FailingTestSideEffect { TargetGrainId = targetId, FailCount = 3 });

        // Pump 1 — attempt 1 fails
        var pump1 = await Pipeline!.PumpOnceAsync();
        pump1.AllSucceeded.Should().BeFalse();

        var statsAfter1 = await storage.GetStats();
        statsAfter1.RetryCount.Should().Be(1);

        await ForceExpireRetryQueue();

        // Pump 2 — attempt 2 fails
        var pump2 = await Pipeline.PumpOnceAsync();
        pump2.AllSucceeded.Should().BeFalse();

        var statsAfter2 = await storage.GetStats();
        statsAfter2.RetryCount.Should().Be(1, "still one entry in retry queue after second failure");

        await ForceExpireRetryQueue();

        // Pump 3 — attempt 3 fails
        var pump3 = await Pipeline.PumpOnceAsync();
        pump3.AllSucceeded.Should().BeFalse();

        await ForceExpireRetryQueue();

        // Pump 4 — attempt 4 succeeds
        var pump4 = await Pipeline.PumpOnceAsync();
        pump4.AllSucceeded.Should().BeTrue();

        var finalStats = await storage.GetStats();
        finalStats.RetryCount.Should().Be(0, "retry queue should be empty after success");
        finalStats.QueueCount.Should().Be(0);
        finalStats.ProcessingCount.Should().Be(0);

        FailingTestSideEffect.GetAttemptCount(targetId).Should().Be(4);
    }

    // --- Mixed batch: some fail, some succeed ---

    [Fact]
    public async Task Batch_MixedResults_SucceededAndFailedTrackedSeparately()
    {
        var storage = GetSiloService<ISideEffectsStorage>();

        var goodTarget1 = Guid.NewGuid();
        var goodTarget2 = Guid.NewGuid();
        var goodTarget3 = Guid.NewGuid();

        // 3 good + 2 always-failing
        await storage.Write(new TestSideEffect { TargetGrainId = goodTarget1 });
        await storage.Write(new AlwaysFailingSideEffect { TrackingId = Guid.NewGuid() });
        await storage.Write(new TestSideEffect { TargetGrainId = goodTarget2 });
        await storage.Write(new AlwaysFailingSideEffect { TrackingId = Guid.NewGuid() });
        await storage.Write(new TestSideEffect { TargetGrainId = goodTarget3 });

        // Pump once — all 5 are picked up, 3 succeed, 2 fail
        var pump = await Pipeline!.PumpOnceAsync();
        pump.TotalTasks.Should().Be(5);
        pump.AllSucceeded.Should().BeFalse();

        var succeeded = pump.Batches.Count(b => b.Success);
        var failed = pump.Batches.Count(b => !b.Success);
        succeeded.Should().Be(3, "three good side effects should succeed");
        failed.Should().Be(2, "two always-failing side effects should fail");

        // Good grains should all be incremented
        (await GetGrain<ITxTestGrain>(goodTarget1).Get()).Should().Be(1);
        (await GetGrain<ITxTestGrain>(goodTarget2).Get()).Should().Be(1);
        (await GetGrain<ITxTestGrain>(goodTarget3).Get()).Should().Be(1);

        // Stats: 2 in retry, 0 in queue/processing
        var stats = await storage.GetStats();
        stats.QueueCount.Should().Be(0);
        stats.ProcessingCount.Should().Be(0);
        stats.RetryCount.Should().Be(2);
    }

    // --- Helpers ---

    private async Task SetProcessingTimestamp(TimeSpan age)
    {
        await using var connection = await Database.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText =
            $"UPDATE side_effects_processing SET processing_started_at = now() - interval '{age.TotalSeconds} seconds'";
        await command.ExecuteNonQueryAsync();
    }

    private async Task ForceExpireRetryQueue()
    {
        await using var connection = await Database.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE side_effects_retry_queue SET retry_after = now() - interval '1 second'";
        await command.ExecuteNonQueryAsync();
        await GetSiloService<ISideEffectsStorage>().RequeueReady();
    }
}