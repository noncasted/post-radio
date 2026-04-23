using FluentAssertions;
using Infrastructure;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

/// <summary>
/// Tests custom transaction system: commit, rollback, concurrent access.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class TransactionTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Transaction_Increment_Success()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        await RunTransaction(() => grain.Increment());

        var value = await grain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task Transaction_MultipleIncrements_AllApplied()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        for (var i = 0; i < 5; i++)
            await RunTransaction(() => grain.Increment());

        var value = await grain.Get();
        value.Should().Be(5);
    }

    [Fact]
    public async Task Transaction_Rollback_ValueUnchanged()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        // First set a known value
        await RunTransaction(() => grain.Increment());

        // Attempt transaction that fails
        var transactions = GetSiloService<ITransactions>();

        var result = await transactions.Run(async () => {
            await grain.Increment();
            throw new Exception("Intentional rollback");
        });

        result.IsSuccess.Should().BeFalse();

        // Value should still be 1 (not 2)
        var value = await grain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task Transaction_Empty_Succeeds()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        // Set a known value before empty transaction
        await RunTransaction(() => grain.Increment());
        var valueBefore = await grain.Get();
        valueBefore.Should().Be(1);

        // Run empty transaction — should succeed without modifying state
        var transactions = GetSiloService<ITransactions>();
        var result = await transactions.Run(() => Task.CompletedTask);
        result.IsSuccess.Should().BeTrue();

        // Verify state was not modified by the empty transaction
        var valueAfter = await grain.Get();
        valueAfter.Should().Be(1);
    }

    [Fact]
    public async Task Transaction_MidChainFail_BothRolledBack()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var grainA = GetGrain<ITxTestGrain>(idA);
        var grainB = GetGrain<ITxTestGrain>(idB);

        var transactions = GetSiloService<ITransactions>();

        var result = await transactions.Run(async () => {
            await grainA.Increment();
            await grainB.Increment();
            throw new Exception("Failure after both grains incremented");
        });

        result.IsSuccess.Should().BeFalse();

        var valueA = await grainA.Get();
        var valueB = await grainB.Get();
        valueA.Should().Be(0);
        valueB.Should().Be(0);

        // Verify grains are usable after rollback
        await RunTransaction(async () => {
            await grainA.Increment();
            await grainB.Increment();
        });

        var finalA = await grainA.Get();
        var finalB = await grainB.Get();
        finalA.Should().Be(1);
        finalB.Should().Be(1);
    }

    [Fact]
    public async Task Transaction_Takeover_SecondTransactionSucceeds()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        var transactions = GetSiloService<ITransactions>();

        // Start slow transaction that holds grain lock for 15s (> 5s StuckGraceSeconds test threshold)
        var slowTask = transactions.Run(() => grain.IncrementWithDelay(15_000));

        // Wait long enough so that by the time the fast transaction's lock-wait
        // times out (LockWaitSeconds=2s), the total elapsed time exceeds StuckGraceSeconds (5s).
        // 4s wait + 2s lock timeout = 6s > 5s grace period → takeover triggers.
        await Task.Delay(4000);

        // Second transaction should wait, then takeover after StuckGraceSeconds
        var fastResult = await transactions.Run(() => grain.Increment());
        fastResult.IsSuccess.Should().BeTrue();

        // Wait for slow transaction to complete (should have been taken over)
        var slowResult = await slowTask;
        slowResult.IsSuccess.Should().BeFalse();

        var value = await grain.Get();
        value.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Transaction_StatePersistsAfterDeactivation()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        await RunTransaction(() => grain.Increment());
        await RunTransaction(() => grain.Increment());
        await RunTransaction(() => grain.Increment());

        // Force grain deactivation — next call will reload from DB
        await grain.Deactivate();
        await Task.Delay(2000);

        var value = await grain.Get();
        value.Should().Be(3);
    }

    [Fact]
    public async Task Transaction_ChainedThreeGrains_AllCommitted()
    {
        var grainA = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var grainB = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var grainC = GetGrain<ITxTestGrain>(Guid.NewGuid());

        await RunTransaction(async () => {
            await grainA.Increment();
            await grainB.Increment();
            await grainC.Increment();
        });

        var valueA = await grainA.Get();
        var valueB = await grainB.Get();
        var valueC = await grainC.Get();

        valueA.Should().Be(1);
        valueB.Should().Be(1);
        valueC.Should().Be(1);
    }

    [Fact]
    public async Task Transaction_ChainedThreeGrainsFail_AllRolledBack()
    {
        var grainA = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var grainB = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var grainC = GetGrain<ITxTestGrain>(Guid.NewGuid());

        var transactions = GetSiloService<ITransactions>();

        var result = await transactions.Run(async () => {
            await grainA.Increment();
            await grainB.Increment();
            await grainC.Increment();
            throw new Exception("Fail after all three grains incremented");
        });

        result.IsSuccess.Should().BeFalse();

        var valueA = await grainA.Get();
        var valueB = await grainB.Get();
        var valueC = await grainC.Get();

        valueA.Should().Be(0);
        valueB.Should().Be(0);
        valueC.Should().Be(0);
    }

    [Fact]
    public async Task Transaction_ConcurrentOnSameGrain_OneFailsOrBothSucceedSequentially()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        var transactions = GetSiloService<ITransactions>();

        // Launch two transactions on the same grain concurrently
        var task1 = transactions.Run(() => grain.Increment());
        var task2 = transactions.Run(() => grain.Increment());

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.IsSuccess);

        // At least one must succeed; both may succeed if serialized properly
        successCount.Should().BeGreaterThanOrEqualTo(1);

        var value = await grain.Get();
        value.Should().Be(successCount);
    }

    [Fact]
    public async Task Transaction_LargeBatch_TenGrainsAllCommitted()
    {
        var grains = Enumerable.Range(0, 10)
                               .Select(_ => GetGrain<ITxTestGrain>(Guid.NewGuid()))
                               .ToList();

        // Verify all grains start at zero
        foreach (var grain in grains)
        {
            var initial = await grain.Get();
            initial.Should().Be(0);
        }

        await RunTransaction(async () => {
            foreach (var grain in grains)
                await grain.Increment();
        });

        // Verify each grain individually was incremented exactly once
        var values = new List<int>();

        foreach (var grain in grains)
        {
            var value = await grain.Get();
            value.Should().Be(1);
            values.Add(value);
        }

        // Verify all 10 committed (not partial)
        values.Should().HaveCount(10);
        values.Should().AllSatisfy(v => v.Should().Be(1));
    }

    [Fact]
    public async Task Transaction_MidChainPartialFail_FirstTwoRolledBack()
    {
        var grainA = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var grainB = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var grainC = GetGrain<ITxTestGrain>(Guid.NewGuid());

        var transactions = GetSiloService<ITransactions>();

        var result = await transactions.Run(async () => {
            await grainA.Increment();
            await grainB.Increment();
            // Fail before touching grainC
            throw new Exception("Fail after two out of three grains");
        });

        result.IsSuccess.Should().BeFalse();

        // Both touched grains must be rolled back
        var valueA = await grainA.Get();
        var valueB = await grainB.Get();
        var valueC = await grainC.Get();

        valueA.Should().Be(0);
        valueB.Should().Be(0);
        valueC.Should().Be(0);

        // Verify all three grains remain usable
        await RunTransaction(async () => {
            await grainA.Increment();
            await grainB.Increment();
            await grainC.Increment();
        });

        (await grainA.Get()).Should().Be(1);
        (await grainB.Get()).Should().Be(1);
        (await grainC.Get()).Should().Be(1);
    }

    [Fact]
    public async Task Transaction_ReturnValue_ReturnsResult()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxSideEffectGrain>(id);

        var transactions = GetSiloService<ITransactions>();
        var result = await transactions.Run(async () => await grain.IncrementAndReturn());

        result.Should().Be(1);

        var value = await grain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task Transaction_ReturnValue_MultipleIncrements()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxSideEffectGrain>(id);

        var transactions = GetSiloService<ITransactions>();

        var r1 = await transactions.Run(async () => await grain.IncrementAndReturn());
        var r2 = await transactions.Run(async () => await grain.IncrementAndReturn());
        var r3 = await transactions.Run(async () => await grain.IncrementAndReturn());

        r1.Should().Be(1);
        r2.Should().Be(2);
        r3.Should().Be(3);
    }

    [Fact]
    public async Task TransactionBuilder_WithCallback_CallbackExecuted()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);
        var callbackExecuted = false;

        var transactions = GetSiloService<ITransactions>();

        var result = await transactions
                           .CreateBuilder(() => grain.Increment())
                           .WithCallback(tx => {
                               callbackExecuted = true;
                               return Task.CompletedTask;
                           })
                           .Run();

        result.IsSuccess.Should().BeTrue();
        callbackExecuted.Should().BeTrue();

        var value = await grain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task TransactionBuilder_MultipleCallbacks_AllExecuted()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);
        var callbackCount = 0;

        var transactions = GetSiloService<ITransactions>();

        var result = await transactions
                           .CreateBuilder(() => grain.Increment())
                           .WithCallback(tx => {
                               callbackCount++;
                               return Task.CompletedTask;
                           })
                           .WithCallback(tx => {
                               callbackCount++;
                               return Task.CompletedTask;
                           })
                           .WithCallback(tx => {
                               callbackCount++;
                               return Task.CompletedTask;
                           })
                           .Run();

        result.IsSuccess.Should().BeTrue();
        callbackCount.Should().Be(3);
    }

    [Fact]
    public async Task Transaction_SameGrainTwice_BothMutationsApplied()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        await RunTransaction(async () => {
            await grain.Increment();
            await grain.Increment();
        });

        var value = await grain.Get();
        value.Should().Be(2);
    }

    [Fact]
    public async Task Transaction_RollbackError_ContainsException()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ITxTestGrain>(id);

        var transactions = GetSiloService<ITransactions>();

        var result = await transactions.Run(async () => {
            await grain.Increment();
            throw new InvalidOperationException("test-error-message");
        });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Contain("test-error-message");
    }
}