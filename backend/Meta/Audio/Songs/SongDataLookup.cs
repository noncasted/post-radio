using System.Text.Encodings.Web;
using System.Text.Json;
using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Meta.Audio;

public interface ISongDataLookup
{
    Task Save(IOperationProgress progress);
    Task Load(IOperationProgress progress);
    string CreateJson();
}

public class SongLookupInfo
{
    public required long Id { get; init; }
    public required string Url { get; init; }
    public required string Author { get; init; }
    public required string Name { get; init; }
    public long? DurationMs { get; init; }
    public bool? IsValid { get; init; }
}

public class SongDataLookup : ISongDataLookup
{
    public SongDataLookup(
        IOrleans orleans,
        ISongsCollection songs,
        IMetaDataCache metadataCache,
        ILogger<SongDataLookup> logger)
    {
        _orleans = orleans;
        _songs = songs;
        _metadataCache = metadataCache;
        _metadataDirectory = Path.GetDirectoryName(_metadataCache.LookupFile) ?? AppContext.BaseDirectory;
        _logger = logger;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IOrleans _orleans;
    private readonly ISongsCollection _songs;
    private readonly IMetaDataCache _metadataCache;
    private readonly ILogger<SongDataLookup> _logger;
    private readonly string _metadataDirectory;

    public async Task Save(IOperationProgress progress)
    {
        progress.SetStatus(OperationStatus.InProgress);
        progress.Log($"Collecting {_songs.Count} songs...");

        var data = CreateLookupData();

        Directory.CreateDirectory(_metadataDirectory);

        progress.Log($"Writing {data.Count} entries to {_metadataCache.LookupFile}");
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(_metadataCache.LookupFile, json);
        _metadataCache.Replace(data);

        _logger.LogInformation("[Audio] [Lookup] Saved {Count} entries to {File}", data.Count, _metadataCache.LookupFile);
        progress.Log($"Saved {data.Count} entries.");
        progress.SetStatus(OperationStatus.Success);
    }

    public string CreateJson()
    {
        var data = CreateLookupData();
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public async Task Load(IOperationProgress progress)
    {
        progress.SetStatus(OperationStatus.InProgress);

        var loadedFromDisk = await _metadataCache.Reload(progress);
        var data = _metadataCache.Snapshot;

        if (data.Count == 0)
        {
            progress.Log(loadedFromDisk
                ? "No entries found."
                : $"No cached entries found for lookup file: {_metadataCache.LookupFile}");
            progress.SetStatus(loadedFromDisk ? OperationStatus.Success : OperationStatus.Failed);
            return;
        }

        progress.Log($"Applying {data.Count} entries...");
        progress.SetProgress(0f);

        var applied = 0;
        var skipped = 0;
        var index = 0;

        foreach (var (id, info) in data)
        {
            index++;

            if (!_songs.TryGetValue(id, out var state))
            {
                skipped++;
                continue;
            }

            var merged = SongMetadataMerge.MergeLookup(id, state, info);

            if (!SongMetadataMerge.HasChanges(state, merged))
            {
                skipped++;
            }
            else
            {
                var grain = _orleans.GetGrain<ISong>(id);
                await grain.UpdateData(merged);
                applied++;
            }

            progress.SetProgress(index / (float)data.Count);

            if (index % 25 == 0 || index == data.Count)
                progress.Log($"Processed {index}/{data.Count} (applied {applied}, skipped {skipped})");
        }

        _logger.LogInformation("[Audio] [Lookup] Applied {Applied}, skipped {Skipped}", applied, skipped);
        progress.Log($"Load complete: applied {applied}, skipped {skipped}.");
        progress.SetStatus(OperationStatus.Success);
    }

    private Dictionary<long, SongLookupInfo> CreateLookupData()
    {
        return _songs.ToDictionary(
            kv => kv.Key,
            kv => new SongLookupInfo
            {
                Id = kv.Key,
                Url = kv.Value.Url,
                Author = kv.Value.Author,
                Name = kv.Value.Name,
                DurationMs = kv.Value.DurationMs,
                IsValid = kv.Value.IsValid
            });
    }
}
