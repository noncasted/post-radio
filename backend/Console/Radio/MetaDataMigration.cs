using System.Text.Json;
using Common.Extensions;
using Infrastructure;
using Meta.Audio;

namespace Console.Radio;

public class MetaDataMigration
{
    public MetaDataMigration(ISongsCollection songsCollection, IOrleans orleans)
    {
        _songsCollection = songsCollection;
        _orleans = orleans;
    }

    private readonly IOrleans _orleans;
    private readonly ISongsCollection _songsCollection;

    public async Task Execute(IOperationProgress progress)
    {
        progress.SetStatus(OperationStatus.InProgress);
        progress.Log("Starting metadata migration...");

        var playlistsFolder = Path.Combine(AppContext.BaseDirectory, "Playlists");

        if (!Directory.Exists(playlistsFolder))
        {
            progress.Log($"Playlists folder not found: {playlistsFolder}");
            progress.SetStatus(OperationStatus.Failed);
            return;
        }

        var jsonFiles = Directory.GetFiles(playlistsFolder, "*-playlist-metadata.json");
        progress.Log($"Found {jsonFiles.Length} metadata files");

        var allEntries = new Dictionary<string, Entry>();

        foreach (var jsonFile in jsonFiles)
        {
            progress.Log($"Reading {Path.GetFileName(jsonFile)}...");
            var jsonContent = await File.ReadAllTextAsync(jsonFile);
            var entries = JsonSerializer.Deserialize<Dictionary<string, Entry>>(jsonContent)!;

            foreach (var (url, entry) in entries)
                allEntries.TryAdd(url, entry);
        }

        progress.Log($"Total entries loaded: {allEntries.Count}");

        var updated = 0;
        var notFound = 0;
        var total = allEntries.Count;
        var processed = 0;

        foreach (var (url, entry) in allEntries)
        {
            var match = _songsCollection.FirstOrDefault(kv => kv.Value.Url == url);

            if (match.Value == null)
            {
                notFound++;
                processed++;
                continue;
            }

            var id = match.Key;
            var state = match.Value;

            var updatedData = new SongData
            {
                Id = id,
                Author = entry.Author,
                Name = entry.Name,
                Url = state.Url,
                Playlists = state.Playlists,
                AddDate = state.AddDate,
                IsLoaded = state.IsLoaded
            };

            var grain = _orleans.GetGrain<ISong>(id);
            await grain.UpdateData(updatedData);
            updated++;

            processed++;
            progress.SetProgress((float)processed / total);
            progress.Log($"Processed {processed}/{total} (Updated: {updated}, Not found: {notFound})");
        }

        progress.Log($"Migration completed! Updated: {updated}, Not found: {notFound}");
        progress.SetStatus(OperationStatus.Success);
    }

    public class Entry
    {
        public required string Url { get; init; }
        public required string Author { get; init; }
        public required string Name { get; init; }
        public required string ShortName { get; init; }
    }
}
