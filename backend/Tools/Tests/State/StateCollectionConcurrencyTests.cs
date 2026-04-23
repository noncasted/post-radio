using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Infrastructure.State;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

/// <summary>
/// Tests StateCollection concurrent updates and edge cases not covered by existing tests.
/// Covers: parallel grain writes, large batch loading, delete-then-reload, key overwrite, incremental load.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class StateCollectionConcurrencyTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Load_ConcurrentGrainUpdates_AllPresent()
    {
        var ids = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();

        await Task.WhenAll(ids.Select(async id => {
            var grain = GetGrain<ICollectionTestGrain>(id);
            await grain.SetName($"concurrent-{id:N}");
        }));

        var utils = GetSiloService<StateCollectionUtils<Guid, CollectionTestState>>();
        var lifetime = new Lifetime();
        var loaded = await utils.Load(lifetime);
        lifetime.Terminate();

        loaded.Count.Should().Be(10);

        foreach (var id in ids)
        {
            loaded.Should().ContainKey(id);
            loaded[id].Name.Should().Be($"concurrent-{id:N}");
        }
    }

    [Fact]
    public async Task Load_LargeBatch_AllEntriesPresent()
    {
        var ids = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList();

        foreach (var id in ids)
        {
            var grain = GetGrain<ICollectionTestGrain>(id);
            await grain.SetName($"batch-{id:N}");
        }

        var utils = GetSiloService<StateCollectionUtils<Guid, CollectionTestState>>();
        var lifetime = new Lifetime();
        var loaded = await utils.Load(lifetime);
        lifetime.Terminate();

        loaded.Count.Should().Be(50);

        foreach (var id in ids)
        {
            loaded.Should().ContainKey(id);
            loaded[id].Name.Should().Be($"batch-{id:N}");
        }
    }

    [Fact]
    public async Task Load_AfterDelete_EntryNotPresent()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ICollectionTestGrain>(id);
        await grain.SetName("to-be-deleted");

        var utils = GetSiloService<StateCollectionUtils<Guid, CollectionTestState>>();

        var lt1 = new Lifetime();
        var before = await utils.Load(lt1);
        lt1.Terminate();

        before.Should().ContainKey(id);

        var storage = GetSiloService<IStateStorage>();
        var stateInfo = storage.Registry.Get<CollectionTestState>();

        await storage.Delete(new StateIdentity
        {
            Key = id,
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        });

        var lt2 = new Lifetime();
        var after = await utils.Load(lt2);
        lt2.Terminate();

        after.Should().NotContainKey(id);
    }

    [Fact]
    public async Task Load_OverwriteSameKey_ReturnsFinalValue()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ICollectionTestGrain>(id);

        await grain.SetName("v1");
        await grain.SetName("v2");
        await grain.SetName("v3");

        var utils = GetSiloService<StateCollectionUtils<Guid, CollectionTestState>>();
        var lifetime = new Lifetime();
        var loaded = await utils.Load(lifetime);
        lifetime.Terminate();

        loaded.Should().ContainKey(id);
        loaded[id].Name.Should().Be("v3");
    }

    [Fact]
    public async Task Load_MixedOldAndNewEntries_SecondLoadHasAll()
    {
        var firstBatch = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var secondBatch = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        foreach (var id in firstBatch)
        {
            var grain = GetGrain<ICollectionTestGrain>(id);
            await grain.SetName($"first-{id:N}");
        }

        var utils = GetSiloService<StateCollectionUtils<Guid, CollectionTestState>>();

        var lt1 = new Lifetime();
        var loaded1 = await utils.Load(lt1);
        lt1.Terminate();

        loaded1.Count.Should().Be(5);

        foreach (var id in firstBatch)
            loaded1.Should().ContainKey(id);

        foreach (var id in secondBatch)
        {
            var grain = GetGrain<ICollectionTestGrain>(id);
            await grain.SetName($"second-{id:N}");
        }

        var lt2 = new Lifetime();
        var loaded2 = await utils.Load(lt2);
        lt2.Terminate();

        loaded2.Count.Should().Be(10);

        foreach (var id in firstBatch)
        {
            loaded2.Should().ContainKey(id);
            loaded2[id].Name.Should().Be($"first-{id:N}");
        }

        foreach (var id in secondBatch)
        {
            loaded2.Should().ContainKey(id);
            loaded2[id].Name.Should().Be($"second-{id:N}");
        }
    }
}