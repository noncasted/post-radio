using System.Text.Json;
using Audio;
using Common;

namespace Console;

public class MetaDataMigration
{
    public MetaDataMigration(ISongsCollection songsCollection, IClusterClient clusterClient)
    {
        _songsCollection = songsCollection;
        _clusterClient = clusterClient;
    }

    private readonly IClusterClient _clusterClient;
    private readonly ISongsCollection _songsCollection;

    public async Task Execute(IOperationProgress progress)
    {
        progress.SetStatus(OperationStatus.InProgress);
        progress.Log("Starting metadata migration...");

        // Получаем путь к папке с JSON файлами
        var playlistsFolder = Path.Combine(AppContext.BaseDirectory, "Playlists");

        if (Directory.Exists(playlistsFolder) == false)
        {
            progress.Log($"Playlists folder not found: {playlistsFolder}");
            return;
        }

        // Находим все JSON файлы
        var jsonFiles = Directory.GetFiles(playlistsFolder, "*-playlist-metadata.json");
        progress.Log($"Found {jsonFiles.Length} metadata files");

        var allEntries = new Dictionary<string, Entry>();

        // Парсим все JSON файлы
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
            var song = _songsCollection.Values.FirstOrDefault(s => s.Url == url);

            if (song == null)
            {
                notFound++;
                continue;
            }

            var songGrain = _clusterClient.GetGrain<ISong>(song.Id);

            var updatedData = new SongData
            {
                Id = song.Id,
                Author = entry.Author,
                Name = entry.Name,
                Url = song.Url,
                Playlists = song.Playlists,
                AddDate = song.AddDate
            };

            await songGrain.UpdateData(updatedData);
            updated++;

            processed++;
            progress.SetProgress((float)processed / total);
            progress.Log($"Processed {processed}/{total} songs (Updated: {updated}, Not found: {notFound})...");
        }

        progress.Log("Refreshing songs collection...");
        await _songsCollection.Refresh();

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