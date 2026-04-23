using System.Text.Encodings.Web;
using System.Text.Json;
using Common;
using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    public bool IsValid { get; init; } = true;
}

public class SongDataLookup : ISongDataLookup
{
    public SongDataLookup(
        IOrleans orleans,
        ISongsCollection songs,
        IOptions<AudioOptions> options,
        ILogger<SongDataLookup> logger)
    {
        _orleans = orleans;
        _songs = songs;
        _lookupFile = ResolveLookupFile(options.Value.SongLookupFile);
        _metadataDirectory = Path.GetDirectoryName(_lookupFile) ?? AppContext.BaseDirectory;
        _logger = logger;
    }

    private const string MetadataDirectoryName = "metadata";
    private const string ToolsDirectoryName = "tools";
    private const string LookupFileName = "songs.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IOrleans _orleans;
    private readonly ISongsCollection _songs;
    private readonly ILogger<SongDataLookup> _logger;
    private readonly string _lookupFile;
    private readonly string _metadataDirectory;

    public async Task Save(IOperationProgress progress)
    {
        progress.SetStatus(OperationStatus.InProgress);
        progress.Log($"Collecting {_songs.Count} songs...");

        var data = CreateLookupData();

        Directory.CreateDirectory(_metadataDirectory);

        progress.Log($"Writing {data.Count} entries to {_lookupFile}");
        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(_lookupFile, json);

        _logger.LogInformation("[Audio] [Lookup] Saved {Count} entries to {File}", data.Count, _lookupFile);
        progress.Log($"Saved {data.Count} entries.");
        progress.SetStatus(OperationStatus.Success);
    }

    public string CreateJson()
    {
        var data = CreateLookupData();
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    private static string ResolveLookupFile(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath));

        var publishedPath = Path.Combine(AppContext.BaseDirectory, ToolsDirectoryName, MetadataDirectoryName, LookupFileName);

        if (File.Exists(publishedPath))
            return Path.GetFullPath(publishedPath);

        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);

        if (repositoryRoot != null)
            return Path.Combine(repositoryRoot, ToolsDirectoryName, MetadataDirectoryName, LookupFileName);

        return Path.GetFullPath(publishedPath);
    }

    private static string? FindRepositoryRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);

        while (directory != null)
        {
            var toolsMetadataPath = Path.Combine(directory.FullName, ToolsDirectoryName, MetadataDirectoryName);
            var backendPath = Path.Combine(directory.FullName, "backend");

            if (Directory.Exists(toolsMetadataPath) && Directory.Exists(backendPath))
                return directory.FullName;

            directory = directory.Parent;
        }

        return null;
    }

    public async Task Load(IOperationProgress progress)
    {
        progress.SetStatus(OperationStatus.InProgress);

        if (!File.Exists(_lookupFile))
        {
            progress.Log($"Lookup file not found: {_lookupFile}");
            progress.SetStatus(OperationStatus.Failed);
            return;
        }

        progress.Log($"Reading {_lookupFile}");
        var json = await File.ReadAllTextAsync(_lookupFile);
        var data = JsonSerializer.Deserialize<Dictionary<long, SongLookupInfo>>(json);

        if (data == null || data.Count == 0)
        {
            progress.Log("No entries found.");
            progress.SetStatus(OperationStatus.Success);
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

            if (state.Author == info.Author
                && state.Name == info.Name
                && state.DurationMs == info.DurationMs
                && state.IsValid == info.IsValid)
            {
                skipped++;
            }
            else
            {
                var grain = _orleans.GetGrain<ISong>(id);
                await grain.UpdateData(new SongData
                {
                    Id = id,
                    Author = info.Author,
                    Name = info.Name,
                    Url = state.Url,
                    Playlists = state.Playlists,
                    AddDate = state.AddDate,
                    IsLoaded = state.IsLoaded,
                    DurationMs = info.DurationMs,
                    IsValid = info.IsValid
                });
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
