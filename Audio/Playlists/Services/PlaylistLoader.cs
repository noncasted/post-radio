using Common;
using Infrastructure.Orleans;
using Microsoft.Extensions.Logging;
using SoundCloudExplode;
using SoundCloudExplode.Common;
using SoundCloudExplode.Exceptions;

namespace Audio;

public class PlaylistLoader : IPlaylistLoader
{
    public PlaylistLoader(
        IOrleans orleans,
        SoundCloudClient soundCloud,
        ISongsCollection songs,
        IObjectStorage objectStorage,
        HttpClient http,
        ILogger<PlaylistLoader> logger)
    {
        _orleans = orleans;
        _soundCloud = soundCloud;
        _songs = songs;
        _objectStorage = objectStorage;
        _http = http;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly SoundCloudClient _soundCloud;
    private readonly ISongsCollection _songs;
    private readonly IObjectStorage _objectStorage;
    private readonly HttpClient _http;
    private readonly ILogger<PlaylistLoader> _logger;

    public async Task Load(PlaylistData playlist, IOperationProgress progress)
    {
        progress.SetStatus(OperationStatus.InProgress);
        progress.Log("Loading playlist tracks...");
        var tracks = await _soundCloud.Playlists.GetTracksAsync(playlist.Url);
        progress.Log($"Received {tracks.Count} tracks from playlist.");

        var allSongs = new List<SongData>();
        var toSetup = new List<SongData>();

        foreach (var track in tracks)
        {
            if (_songs.TryGetValue(track.Id, out var data) == true)
            {
                allSongs.Add(data);
                continue;
            }

            var author = string.Empty;
            var name = string.Empty;

            if (track.PublisherMetadata is { Artist: not null })
                author = track.PublisherMetadata.Artist;

            if (track.Title != null)
                name = track.Title;

            data = new SongData
            {
                Id = track.Id,
                Url = track.PermalinkUrl!.ToString(),
                Playlists = new[]
                {
                    playlist.Id
                },
                Author = author,
                Name = name,
                AddDate = DateTime.UtcNow,
            };

            toSetup.Add(data);
            allSongs.Add(data);
        }

        progress.Log($"Checking {allSongs.Count} for download...");
        var songKeys = allSongs.Select(s => s.Id.ToString()).ToList();
        var existingSongs = await _objectStorage.ContainsMany("audio", songKeys);

        var toDownload = allSongs
            .Where(song => !existingSongs.Contains(song.Id.ToString()))
            .ToList();

        progress.Log($"Downloading {toDownload.Count}");
        progress.SetProgress(0f);
        var unavailable = new List<SongData>();

        for (var index = 0; index < toDownload.Count; index++)
        {
            var song = toDownload[index];

            try
            {
                await Download(song);
            }
            catch (TrackUnavailableException)
            {
                unavailable.Add(song);
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    "[Audio] [Playlist] Failed to download track {Author} - {Name} from playlist {PlaylistId}",
                    song.Author, song.Name, playlist.Id
                );
            }

            progress.SetProgress(index / (float)toDownload.Count);
            progress.Log($"Downloaded {song.Id} {index + 1} / {toDownload.Count}");
        }

        foreach (var song in unavailable)
        {
            allSongs.Remove(song);
            toSetup.Remove(song);
        }

        progress.Log($"Found {toSetup.Count} new to setup.");

        for (var index = 0; index < toSetup.Count; index++)
        {
            progress.SetProgress(index / (float)toSetup.Count);
            progress.Log($"Setup {index + 1} / {toSetup.Count}");
            var song = toSetup[index];

            var grain = _orleans.GetGrain<ISong>(song.Id);
            await grain.UpdateData(song);
        }

        progress.Log($"Found {allSongs.Count} to process.");

        var addPlaylist = new List<SongData>();

        foreach (var song in allSongs)
        {
            if (song.Playlists.Contains(playlist.Id) == true)
                continue;

            addPlaylist.Add(song);
        }

        progress.Log($"Adding playlist to {addPlaylist.Count}");
        progress.SetProgress(0f);

        for (var index = 0; index < addPlaylist.Count; index++)
        {
            var song = addPlaylist[index];
            var grain = _orleans.GetGrain<ISong>(song.Id);
            await grain.AddToPlaylist(playlist.Id);
            progress.SetProgress(index / (float)addPlaylist.Count);
            progress.Log($"Set playlist for {index + 1} / {addPlaylist.Count}");
        }

        progress.SetStatus(OperationStatus.Success);

        await _songs.Refresh();
    }

    private async Task Download(SongData data)
    {
        _logger.LogInformation("[Audio] [Playlist] Downloading track {Author} {Name}", data.Author, data.Name);

        var mp3TrackMediaUrl = await _soundCloud.Tracks.GetDownloadUrlAsync(data.Url)!;

        var request = new HttpRequestMessage(HttpMethod.Get, mp3TrackMediaUrl);

        var response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead
        );

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode})." +
                Environment.NewLine +
                "Request:" +
                Environment.NewLine +
                request
            );
        }

        var stream = await response.Content.ReadAsStreamAsync();

        var totalLength = response.Content.Headers.ContentLength ?? 0;
        var destination = new MemoryStream();
        await stream.CopyToAsync(destination, (int)totalLength, CancellationToken.None);

        // Upload to MinIO before disposing the stream
        await _objectStorage.Put("audio", data.Id.ToString(), destination, "audio/mpeg");
    }
}