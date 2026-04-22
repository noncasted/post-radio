using Infrastructure;

namespace Benchmarks;

public class TestParticipants
{
    public required IOrleans Orleans { get; init; }
    public required int Count { get; init; }
    public required IReadOnlyList<Guid> Entries { get; init; }

    public async Task<IReadOnlyList<TValue>> Get<TValue, TGrain>(Func<TGrain, Task<TValue>> func)
        where TGrain : IGrainWithGuidKey
    {
        var result = new List<TValue>();

        foreach (var id in Entries)
        {
            var grain = Orleans.GetGrain<TGrain>(id);
            var value = await func(grain);
            result.Add(value);
        }

        return result;
    }

    public async Task Run<TGrain>(Func<TGrain, Task> func)
        where TGrain : IGrainWithGuidKey
    {
        foreach (var id in Entries)
        {
            var grain = Orleans.GetGrain<TGrain>(id);
            await func(grain);
        }
    }

    public static TestParticipants Create(IOrleans orlens, int count)
    {
        var entries = new Guid[count];

        for (var i = 0; i < count; i++)
            entries[i] = Guid.NewGuid();

        return new TestParticipants
        {
            Orleans = orlens,
            Count = count,
            Entries = entries
        };
    }
}