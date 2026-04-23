using Common.Reactive;
using FluentAssertions;
using Infrastructure.State;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

/// <summary>
/// Tests StateStorage batch operations: ReadBatch and WriteBatch (via StateWriteRequest with multiple records).
/// Basic Write/Read/Delete/ReadAll are covered by StateReadWriteTests.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class StateStorageBatchTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task ReadBatch_MultipleEntries_ReturnsAll()
    {
        var storage = GetSiloService<IStateStorage>();
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        foreach (var id in ids)
        {
            var grain = GetGrain<ISimpleTestGrain>(id);
            await grain.SetCounter(id.GetHashCode());
        }

        var stateInfo = storage.Registry.Get<SimpleTestState>();

        var identities = ids.Select(id => new StateIdentity
        {
            Key = id,
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        }).ToList();

        var result = await storage.ReadBatch<Guid, SimpleTestState>(identities);

        result.Count.Should().Be(5);

        foreach (var id in ids)
        {
            result.Should().ContainKey(id);
            result[id].Counter.Should().Be(id.GetHashCode());
        }
    }

    [Fact]
    public async Task ReadBatch_EmptyList_ReturnsEmptyDictionary()
    {
        var storage = GetSiloService<IStateStorage>();

        var result = await storage.ReadBatch<Guid, SimpleTestState>([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadBatch_PartialMiss_ReturnsOnlyExistingKeys()
    {
        var storage = GetSiloService<IStateStorage>();

        var existingId = Guid.NewGuid();
        var missingId = Guid.NewGuid();

        var grain = GetGrain<ISimpleTestGrain>(existingId);
        await grain.SetCounter(77);

        var stateInfo = storage.Registry.Get<SimpleTestState>();

        var identities = new List<StateIdentity>
        {
            new()
            {
                Key = existingId,
                Type = stateInfo.Name,
                TableName = stateInfo.TableName,
                Extension = null
            },
            new()
            {
                Key = missingId,
                Type = stateInfo.Name,
                TableName = stateInfo.TableName,
                Extension = null
            }
        };

        var result = await storage.ReadBatch<Guid, SimpleTestState>(identities);

        result.Should().ContainKey(existingId);
        result[existingId].Counter.Should().Be(77);
        result.Should().NotContainKey(missingId);
    }

    [Fact]
    public async Task ReadAll_CancellationViaLifetime_StopsIterationWithoutError()
    {
        var storage = GetSiloService<IStateStorage>();

        // Seed a few entries so iteration has something to yield
        for (var i = 0; i < 3; i++)
        {
            var grain = GetGrain<ISimpleTestGrain>(Guid.NewGuid());
            await grain.SetCounter(i);
        }

        var lifetime = new Lifetime();
        var collected = new List<(Guid, SimpleTestState)>();

        var act = async () => {
            await foreach (var entry in storage.ReadAll<Guid, SimpleTestState>(lifetime))
            {
                collected.Add(entry);
                // Terminate after the first result — simulates mid-stream cancellation
                lifetime.Terminate();
            }
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReadAll_EmptyTable_YieldsNothing()
    {
        var storage = GetSiloService<IStateStorage>();

        // TxTestState table is not written to by any other test in this class
        var lifetime = new Lifetime();
        var results = new List<(Guid, TxTestState)>();

        await foreach (var entry in storage.ReadAll<Guid, TxTestState>(lifetime))
        {
            results.Add(entry);
        }

        lifetime.Terminate();

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task WriteBatch_MultipleRecordsAtOnce_AllReadBack()
    {
        var storage = GetSiloService<IStateStorage>();
        var stateInfo = storage.Registry.Get<SimpleTestState>();

        var entries = Enumerable.Range(0, 4)
                                .Select(i => (id: Guid.NewGuid(), counter: i * 10, label: $"batch-{i}"))
                                .ToList();

        var records = entries.ToDictionary(e => new StateIdentity
            {
                Key = e.id,
                Type = stateInfo.Name,
                TableName = stateInfo.TableName,
                Extension = null
            },
            e => (IStateValue)new SimpleTestState { Counter = e.counter, Label = e.label });

        await storage.Write(new StateWriteRequest { Records = records });

        foreach (var (id, counter, label) in entries)
        {
            var identity = new StateIdentity
            {
                Key = id,
                Type = stateInfo.Name,
                TableName = stateInfo.TableName,
                Extension = null
            };
            var read = await storage.Read<SimpleTestState>(identity);
            read.Counter.Should().Be(counter);
            read.Label.Should().Be(label);
        }
    }
}