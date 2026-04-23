using FluentAssertions;
using Infrastructure;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

/// <summary>
/// Tests side effect registration, execution, and pipeline drain.
/// </summary>
[Collection(nameof(SideEffectIntegrationCollection))]
public class SideEffectTests(SideEffectTestFixture fixture) : IntegrationTestBase<SideEffectTestFixture>(fixture)
{
    [Fact]
    public async Task SideEffect_RegisterAndDrain_ExecutesTargetGrain()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        // Register a side effect that increments the target grain
        var sourceGrain = GetGrain<ISideEffectTestGrain>(sourceId);
        await sourceGrain.RegisterSideEffect(targetId);

        // Drain side effects
        var result = await DrainSideEffectsAsync();
        result.AssertDrainedWithWork();

        // Verify target grain was incremented
        var targetGrain = GetGrain<ITxTestGrain>(targetId);
        var value = await targetGrain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task SideEffect_MultipleDrain_AllExecuted()
    {
        var targetId = Guid.NewGuid();

        // Register 3 side effects targeting the same grain
        for (var i = 0; i < 3; i++)
        {
            var sourceGrain = GetGrain<ISideEffectTestGrain>(Guid.NewGuid());
            await sourceGrain.RegisterSideEffect(targetId);
        }

        // Drain all
        var result = await DrainSideEffectsAsync();
        result.AssertDrainedSuccessfully();

        // Verify target was incremented 3 times
        var targetGrain = GetGrain<ITxTestGrain>(targetId);
        var value = await targetGrain.Get();
        value.Should().Be(3);
    }

    [Fact]
    public async Task SideEffect_Transactional_ExecutesInsideTransaction()
    {
        var targetId = Guid.NewGuid();
        var storage = GetSiloService<ISideEffectsStorage>();

        await storage.Write(new TransactionalTestSideEffect { TargetGrainId = targetId });

        var result = await DrainSideEffectsAsync();
        result.AssertDrainedWithWork();

        var targetGrain = GetGrain<ITxTestGrain>(targetId);
        var value = await targetGrain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task SideEffect_Transactional_MultipleDrain_AllExecuted()
    {
        // Use separate targets to verify each transactional effect commits independently
        var targetIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();
        var storage = GetSiloService<ISideEffectsStorage>();

        foreach (var targetId in targetIds)
            await storage.Write(new TransactionalTestSideEffect { TargetGrainId = targetId });

        var result = await DrainSideEffectsAsync();
        result.AssertDrainedSuccessfully();

        // Verify each target was incremented exactly once (atomic per-effect)
        foreach (var targetId in targetIds)
        {
            var targetGrain = GetGrain<ITxTestGrain>(targetId);
            var value = await targetGrain.Get();

            value.Should()
                 .Be(1, $"target {targetId} should be incremented exactly once by its transactional side effect");
        }
    }

    [Fact]
    public async Task SideEffect_FailAndRetry_EventuallySucceeds()
    {
        FailingTestSideEffect.ResetAttempts();
        var targetId = Guid.NewGuid();
        var storage = GetSiloService<ISideEffectsStorage>();

        // SE that fails 2 times then succeeds on 3rd attempt
        await storage.Write(new FailingTestSideEffect { TargetGrainId = targetId, FailCount = 2 });

        // First pump — fails, goes to retry queue
        var pump1 = await Pipeline!.PumpOnceAsync();
        pump1.AllSucceeded.Should().BeFalse();

        // Wait for retry delay to expire, then requeue and pump again
        // IncrementalRetryDelay from config * (retryCount+1) — we need to wait or manipulate time.
        // Since test config has IncrementalRetryDelay=30s, we can't wait that long.
        // Instead, directly call RequeueStuck to simulate time passage — move processing→queue
        // Actually FailProcessing already moved it to retry_queue. We need RequeueReady but retry_after is in the future.
        // Workaround: pump multiple times with RequeueStuck between to force reprocessing.

        // The entry is now in retry_queue with retry_after in the future.
        // We'll use direct DB access to force-expire the retry_after.
        await ForceExpireRetryQueue();

        // Second pump — fails again (attempt 2)
        var pump2 = await Pipeline.PumpOnceAsync();
        pump2.AllSucceeded.Should().BeFalse();

        await ForceExpireRetryQueue();

        // Third pump — succeeds (attempt 3)
        var pump3 = await Pipeline.PumpOnceAsync();
        pump3.AllSucceeded.Should().BeTrue();

        var targetGrain = GetGrain<ITxTestGrain>(targetId);
        var value = await targetGrain.Get();
        value.Should().Be(1);

        FailingTestSideEffect.GetAttemptCount(targetId).Should().Be(3);
    }

    [Fact]
    public async Task SideEffect_MaxRetriesExceeded_Dropped()
    {
        var trackingId = Guid.NewGuid();
        var storage = GetSiloService<ISideEffectsStorage>();
        var config = GetSiloService<ISideEffectsConfig>();
        var maxRetries = config.Value.MaxRetryCount;

        await storage.Write(new AlwaysFailingSideEffect { TrackingId = trackingId });

        // Pump + force-expire retry for each attempt
        for (var i = 0; i < maxRetries + 1; i++)
        {
            await Pipeline!.PumpOnceAsync();
            await ForceExpireRetryQueue();
        }

        // After max retries, the entry should be deleted — no more work
        var finalPump = await Pipeline!.PumpOnceAsync();
        finalPump.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task SideEffect_RequeueStuck_RecoversCrashedEntries()
    {
        var targetId = Guid.NewGuid();
        var storage = GetSiloService<ISideEffectsStorage>();

        await storage.Write(new TestSideEffect { TargetGrainId = targetId });

        // Read moves entry from queue → processing (simulates worker picking it up)
        var entries = await storage.Read(1);
        entries.Should().HaveCount(1);

        // Queue should be empty now
        var emptyRead = await storage.Read(1);
        emptyRead.Should().BeEmpty();

        // Simulate crash recovery — move processing → queue
        await storage.RequeueStuck();

        // Now the entry should be back in the queue
        var result = await DrainSideEffectsAsync();
        result.AssertDrainedWithWork();

        var targetGrain = GetGrain<ITxTestGrain>(targetId);
        var value = await targetGrain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task SideEffect_WorkerExceptionIsolation_OneFailureDoesNotCrashOthers()
    {
        var goodTargetId = Guid.NewGuid();
        var storage = GetSiloService<ISideEffectsStorage>();

        // Write one always-failing effect and one good effect
        await storage.Write(new AlwaysFailingSideEffect { TrackingId = Guid.NewGuid() });
        await storage.Write(new TestSideEffect { TargetGrainId = goodTargetId });

        // Pump once — both should be picked up, one fails, one succeeds
        var result = await Pipeline!.PumpOnceAsync();
        result.TotalTasks.Should().Be(2);
        result.AllSucceeded.Should().BeFalse();

        // The good side effect should have executed despite the other failing
        var targetGrain = GetGrain<ITxTestGrain>(goodTargetId);
        var value = await targetGrain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task SideEffect_EmptyQueue_DrainReturnsQuiet()
    {
        // No side effects registered — drain should return quietly
        var result = await Pipeline!.DrainUntilQuietAsync();
        result.ReachedQuiescence.Should().BeTrue();
        result.TotalTasks.Should().Be(0);
    }

    [Fact]
    public async Task SideEffect_RegisteredViaAddToTransaction_ExecutedAfterCommit()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var grain = GetGrain<ITxSideEffectGrain>(sourceId);

        await RunTransaction(() => grain.IncrementAndRegisterSideEffect(targetId));

        // Source grain should have been incremented
        var sourceValue = await grain.Get();
        sourceValue.Should().Be(1);

        // Drain side effects
        var result = await DrainSideEffectsAsync();
        result.AssertDrainedWithWork();

        // Target grain should have been incremented by the side effect
        var targetGrain = GetGrain<ITxTestGrain>(targetId);
        var targetValue = await targetGrain.Get();
        targetValue.Should().Be(1);
    }

    [Fact]
    public async Task SideEffect_MultipleSideEffectsInSingleTransaction_AllExecuted()
    {
        var sourceId = Guid.NewGuid();
        var targetIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        var grain = GetGrain<ITxSideEffectGrain>(sourceId);

        await RunTransaction(() => grain.RegisterMultipleSideEffects(targetIds));

        var result = await DrainSideEffectsAsync();
        result.AssertDrainedSuccessfully();

        foreach (var targetId in targetIds)
        {
            var targetGrain = GetGrain<ITxTestGrain>(targetId);
            var value = await targetGrain.Get();
            value.Should().Be(1);
        }
    }

    [Fact]
    public async Task SideEffect_TransactionRollback_SideEffectNotEnqueued()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var grain = GetGrain<ITxSideEffectGrain>(sourceId);

        // Transaction that registers a side effect then fails
        var transactions = GetSiloService<ITransactions>();

        var txResult = await transactions.Run(async () => {
            await grain.IncrementAndRegisterSideEffect(targetId);
            throw new Exception("Intentional failure after side effect registration");
        });

        txResult.IsSuccess.Should().BeFalse();

        // Drain — should find nothing because the transaction was rolled back
        var drainResult = await Pipeline!.DrainUntilQuietAsync();
        drainResult.TotalTasks.Should().Be(0);

        // Target grain should not have been touched
        var targetGrain = GetGrain<ITxTestGrain>(targetId);
        var targetValue = await targetGrain.Get();
        targetValue.Should().Be(0);
    }

    [Fact]
    public async Task SideEffect_TransactionalFails_RetryAndEventualSuccess()
    {
        FailingTestSideEffect.ResetAttempts();
        var targetId = Guid.NewGuid();
        var storage = GetSiloService<ISideEffectsStorage>();

        // Transactional side effect that fails once then succeeds
        await storage.Write(new FailingTransactionalTestSideEffect { TargetGrainId = targetId, FailCount = 1 });

        // First pump — fails
        var pump1 = await Pipeline!.PumpOnceAsync();
        pump1.AllSucceeded.Should().BeFalse();

        await ForceExpireRetryQueue();

        // Second pump — succeeds
        var pump2 = await Pipeline.PumpOnceAsync();
        pump2.AllSucceeded.Should().BeTrue();

        var targetGrain = GetGrain<ITxTestGrain>(targetId);
        var value = await targetGrain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task SideEffect_BatchFromDifferentSources_AllProcessed()
    {
        // Use separate targets per effect type to verify each type executes correctly
        var targetNonTx1 = Guid.NewGuid();
        var targetTx = Guid.NewGuid();
        var targetNonTx2 = Guid.NewGuid();
        var storage = GetSiloService<ISideEffectsStorage>();

        // Write different types of side effects targeting different grains
        await storage.Write(new TestSideEffect { TargetGrainId = targetNonTx1 });
        await storage.Write(new TransactionalTestSideEffect { TargetGrainId = targetTx });
        await storage.Write(new TestSideEffect { TargetGrainId = targetNonTx2 });

        var result = await DrainSideEffectsAsync();
        result.AssertDrainedSuccessfully();

        // Verify each target grain received exactly one increment
        var valueNonTx1 = await GetGrain<ITxTestGrain>(targetNonTx1).Get();
        var valueTx = await GetGrain<ITxTestGrain>(targetTx).Get();
        var valueNonTx2 = await GetGrain<ITxTestGrain>(targetNonTx2).Get();

        valueNonTx1.Should().Be(1, "first non-transactional side effect should increment its target");
        valueTx.Should().Be(1, "transactional side effect should increment its target");
        valueNonTx2.Should().Be(1, "second non-transactional side effect should increment its target");
    }

    /// <summary>
    /// Force-expire all entries in retry queue by setting retry_after to past.
    /// Then call RequeueReady to move them back to main queue.
    /// </summary>
    private async Task ForceExpireRetryQueue()
    {
        await using var connection = await Database.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE side_effects_retry_queue SET retry_after = now() - interval '1 second'";
        await command.ExecuteNonQueryAsync();
        await GetSiloService<ISideEffectsStorage>().RequeueReady();
    }
}