using Common.Reactive;
using FluentAssertions;
using Infrastructure.State;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

/// <summary>
/// Tests basic grain state read/write operations via the real Orleans TestCluster.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class StateReadWriteTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task SimpleGrain_WriteAndRead_ReturnsSameValue()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ISimpleTestGrain>(id);

        await grain.SetCounter(42);
        var result = await grain.GetCounter();

        result.Should().Be(42);
    }

    [Fact]
    public async Task SimpleGrain_WriteLabel_ReturnsSameLabel()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ISimpleTestGrain>(id);

        await grain.SetLabel("hello-world");
        var label = await grain.GetLabel();

        label.Should().Be("hello-world");
    }

    [Fact]
    public async Task SimpleGrain_MultipleWrites_KeepsLatest()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<ISimpleTestGrain>(id);

        await grain.SetCounter(1);
        await grain.SetCounter(2);
        await grain.SetCounter(3);
        var result = await grain.GetCounter();

        result.Should().Be(3);
    }

    [Fact]
    public async Task SimpleGrain_DifferentGrains_IndependentState()
    {
        var grain1 = GetGrain<ISimpleTestGrain>(Guid.NewGuid());
        var grain2 = GetGrain<ISimpleTestGrain>(Guid.NewGuid());

        await grain1.SetCounter(100);
        await grain2.SetCounter(200);

        var result1 = await grain1.GetCounter();
        var result2 = await grain2.GetCounter();

        result1.Should().Be(100);
        result2.Should().Be(200);
    }

    [Fact]
    public async Task StateStorage_WriteAndRead_RoundTripPreservesData()
    {
        var storage = GetSiloService<IStateStorage>();
        var id = Guid.NewGuid();

        var stateInfo = storage.Registry.Get<SimpleTestState>();

        var identity = new StateIdentity
        {
            Key = id,
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        };

        var written = new SimpleTestState { Counter = 99, Label = "round-trip" };
        await storage.Write(identity, written);

        var read = await storage.Read<SimpleTestState>(identity);

        read.Counter.Should().Be(99);
        read.Label.Should().Be("round-trip");
    }

    [Fact]
    public async Task StateStorage_Delete_RemovesEntry()
    {
        var storage = GetSiloService<IStateStorage>();
        var id = Guid.NewGuid();

        var stateInfo = storage.Registry.Get<SimpleTestState>();

        var identity = new StateIdentity
        {
            Key = id,
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        };

        await storage.Write(identity, new SimpleTestState { Counter = 50 });
        await storage.Delete(identity);

        var read = await storage.Read<SimpleTestState>(identity);

        // After delete, reading returns default (new T())
        read.Counter.Should().Be(0);
        read.Label.Should().BeEmpty();
    }

    [Fact]
    public async Task StateStorage_ReadAll_ReturnsAllEntries()
    {
        var storage = GetSiloService<IStateStorage>();

        // Write multiple entries via grains so they land in the DB
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        foreach (var id in ids)
        {
            var grain = GetGrain<ICollectionTestGrain>(id);
            await grain.SetName($"item-{id:N}");
        }

        // ReadAll for CollectionTestState
        var lifetime = new Lifetime();
        var results = new List<(Guid, CollectionTestState)>();

        await foreach (var entry in storage.ReadAll<Guid, CollectionTestState>(lifetime))
        {
            results.Add(entry);
        }

        lifetime.Terminate();

        results.Count.Should().BeGreaterThanOrEqualTo(3);

        foreach (var id in ids)
        {
            results.Should().Contain(r => r.Item1 == id && r.Item2.Name == $"item-{id:N}");
        }
    }

    [Fact]
    public async Task StateStorage_DeleteMultiple_RemovesAllSpecified()
    {
        var storage = GetSiloService<IStateStorage>();

        var stateInfo = storage.Registry.Get<SimpleTestState>();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var identities = ids.Select(id => new StateIdentity
                            {
                                Key = id,
                                Type = stateInfo.Name,
                                TableName = stateInfo.TableName,
                                Extension = null
                            })
                            .ToList();

        foreach (var identity in identities)
        {
            await storage.Write(identity, new SimpleTestState { Counter = 1 });
        }

        await storage.Delete(identities);

        foreach (var identity in identities)
        {
            var read = await storage.Read<SimpleTestState>(identity);
            read.Counter.Should().Be(0);
        }
    }
}