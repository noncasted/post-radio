using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

/// <summary>
/// Tests StateCollection sync: load from DB, verify persistence through grain writes.
/// Note: PushUpdate/PushTransactionalUpdate require the full side effects pipeline
/// (DurableQueue + SideEffectsProcessor), which is tested separately in SideEffectTests.
/// These tests verify the DB-backed load path and grain-state-to-collection consistency.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class StateCollectionTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task StateCollectionUtils_Load_ReturnsEntriesFromDatabase()
    {
        // Write entries via grains — they persist to DB
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var grain1 = GetGrain<ICollectionTestGrain>(id1);
        await grain1.SetName("alpha");

        var grain2 = GetGrain<ICollectionTestGrain>(id2);
        await grain2.SetName("beta");

        // Load collection from DB using StateCollectionUtils
        var utils = GetSiloService<StateCollectionUtils<Guid, CollectionTestState>>();
        var lifetime = new Lifetime();

        var loaded = await utils.Load(lifetime);

        lifetime.Terminate();

        loaded.Should().ContainKey(id1);
        loaded.Should().ContainKey(id2);
        loaded[id1].Name.Should().Be("alpha");
        loaded[id2].Name.Should().Be("beta");
    }

    [Fact]
    public async Task StateCollectionUtils_Load_ReflectsGrainUpdates()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ICollectionTestGrain>(id);
        await grain.SetName("initial");

        // Load, verify initial
        var utils = GetSiloService<StateCollectionUtils<Guid, CollectionTestState>>();
        var lt1 = new Lifetime();
        var loaded1 = await utils.Load(lt1);
        lt1.Terminate();

        loaded1.Should().ContainKey(id);
        loaded1[id].Name.Should().Be("initial");

        // Update via grain
        await grain.SetName("updated");

        // Reload — should reflect the change
        var lt2 = new Lifetime();
        var loaded2 = await utils.Load(lt2);
        lt2.Terminate();

        loaded2[id].Name.Should().Be("updated");
    }

    [Fact]
    public async Task StateCollectionUtils_Load_EmptyDatabase_ReturnsEmpty()
    {
        var utils = GetSiloService<StateCollectionUtils<Guid, CollectionTestState>>();
        var lifetime = new Lifetime();

        var loaded = await utils.Load(lifetime);

        lifetime.Terminate();

        loaded.Should().NotBeNull();
        loaded.Should().BeEmpty();
        loaded.Count.Should().Be(0);
        loaded.Should().BeAssignableTo<IReadOnlyDictionary<Guid, CollectionTestState>>();

        // Iterating empty collection should not throw
        foreach (var _ in loaded)
        {
            throw new Exception("Should not iterate any entries");
        }
    }

    [Fact]
    public async Task StateCollectionUtils_Load_MultipleGrains_AllPresent()
    {
        var utils = GetSiloService<StateCollectionUtils<Guid, CollectionTestState>>();

        // Write several entries
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        foreach (var id in ids)
        {
            var grain = GetGrain<ICollectionTestGrain>(id);
            await grain.SetName($"item-{id:N}");
        }

        var lifetime = new Lifetime();
        var loaded = await utils.Load(lifetime);
        lifetime.Terminate();

        loaded.Count.Should().Be(5);

        foreach (var id in ids)
        {
            loaded.Should().ContainKey(id);
            loaded[id].Id.Should().Be(id);
            loaded[id].Name.Should().Be($"item-{id:N}");
            loaded[id].Name.Should().StartWith("item-");
        }

        // Verify no extra entries leaked in
        loaded.Keys.Should().BeEquivalentTo(ids);
    }
}