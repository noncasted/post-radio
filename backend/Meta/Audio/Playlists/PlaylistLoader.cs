using Common;
using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Logging;
using SoundCloudExplode;
using SoundCloudExplode.Exceptions;

namespace Meta.Audio;

public interface IPlaylistLoader
{
    Task Fetch(PlaylistData playlist, IOperationProgress progress);
    Task Load(PlaylistData playlist, IOperationProgress progress);
}

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

    private readonly HttpClient _http;
    private readonly ILogger<PlaylistLoader> _logger;
    private readonly IObjectStorage _objectStorage;
    private readonly IOrleans _orleans;
    private readonly ISongsCollection _songs;
    private readonly SoundCloudClient _soundCloud;

    public async Task Fetch(PlaylistData playlist, IOperationProgress progress)
    {
        progress.SetStatus(OperationStatus.InProgress);
        progress.Log("Fetching playlist tracks...");

        _logger.LogInformation(
            "[Audio] [Playlist] Fetch start. Url='{Url}', SoundCloudClient.ClientId='{ClientId}'",
            playlist.Url,
            _soundCloud.ClientId ?? "<null>");

        var tracks = new List<SoundCloudExplode.Tracks.Track>();

        try
        {
            await foreach (var track in _soundCloud.Playlists.GetTracksAsync(playlist.Url))
                tracks.Add(track);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "[Audio] [Playlist] Fetch FAILED at GetTracksAsync. Url='{Url}', ClientId='{ClientId}'. " +
                "If this is a 401 after a proxy is configured, the tunnel is probably up but the extracted ClientId is stale — the SoundCloudClient was likely initialised before the proxy was applied, or a prior init grabbed a bad client_id.",
                playlist.Url, _soundCloud.ClientId ?? "<null>");
            throw;
        }

        progress.Log($"Received {tracks.Count} tracks from playlist.");

        var toCreate = new List<SongData>();
        var toAttach = new List<long>();

        foreach (var track in tracks)
        {
            if (_songs.TryGetValue(track.Id, out var existing))
            {
                if (!existing.Playlists.Contains(playlist.Id))
                    toAttach.Add(track.Id);
                continue;
            }

            var author = track.PublisherMetadata is { Artist: not null }
                ? track.PublisherMetadata.Artist
                : string.Empty;
            var name = track.Title ?? string.Empty;

            toCreate.Add(new SongData
            {
                Id = track.Id,
                Url = track.PermalinkUrl!.ToString(),
                Playlists = new[] { playlist.Id },
                Author = author,
                Name = name,
                AddDate = DateTime.UtcNow,
                IsLoaded = false
            });
        }

        progress.Log($"Creating {toCreate.Count} new song grains...");
        progress.SetProgress(0f);

        for (var i = 0; i < toCreate.Count; i++)
        {
            var data = toCreate[i];
            var grain = _orleans.GetGrain<ISong>(data.Id);
            await grain.UpdateData(data);

            progress.SetProgress(i / (float)Math.Max(1, toCreate.Count));
            progress.Log($"Created {i + 1} / {toCreate.Count}: {data.Author} - {data.Name}");
        }

        progress.Log($"Attaching playlist to {toAttach.Count} existing songs...");
        progress.SetProgress(0f);

        for (var i = 0; i < toAttach.Count; i++)
        {
            var grain = _orleans.GetGrain<ISong>(toAttach[i]);
            await grain.AddToPlaylist(playlist.Id);

            progress.SetProgress(i / (float)Math.Max(1, toAttach.Count));
            progress.Log($"Attached {i + 1} / {toAttach.Count}");
        }

        progress.Log($"Fetch complete: {toCreate.Count} new, {toAttach.Count} attached.");
        progress.SetStatus(OperationStatus.Success);
    }

    public async Task Load(PlaylistData playlist, IOperationProgress progress)
    {
        progress.SetStatus(OperationStatus.InProgress);
        progress.Log("Scanning playlist for unloaded songs...");

        var pending = _songs
                      .Where(kv => kv.Value.Playlists.Contains(playlist.Id) && !kv.Value.IsLoaded)
                      .Select(kv => (Id: kv.Key, State: kv.Value))
                      .ToList();

        progress.Log($"Found {pending.Count} unloaded songs.");
        progress.SetProgress(0f);

        var downloaded = 0;
        var failed = 0;

        for (var i = 0; i < pending.Count; i++)
        {
            var (id, state) = pending[i];

            try
            {
                await Download(id, state);
                await _orleans.GetGrain<ISong>(id).SetLoaded(true);
                downloaded++;
                progress.Log($"Loaded {i + 1} / {pending.Count}: {state.Author} - {state.Name}");
            }
            catch (TrackUnavailableException)
            {
                failed++;
                progress.Log($"Unavailable {i + 1} / {pending.Count}: {state.Author} - {state.Name}");
            }
            catch (Exception e)
            {
                failed++;

                _logger.LogError(e, "[Audio] [Playlist] Failed to download {Author} - {Name} (playlist {PlaylistId})",
                    state.Author, state.Name, playlist.Id);
                progress.Log($"Failed {i + 1} / {pending.Count}: {e.Message}");
            }

            progress.SetProgress((i + 1) / (float)Math.Max(1, pending.Count));
        }

        progress.Log($"Load complete: {downloaded} downloaded, {failed} failed.");
        progress.SetStatus(OperationStatus.Success);
    }

    private async Task Download(long id, SongState state)
    {
        _logger.LogInformation("[Audio] [Playlist] Downloading {Author} {Name}", state.Author, state.Name);

        var mediaUrl = await _soundCloud.Tracks.GetDownloadUrlAsync(state.Url);

        var request = new HttpRequestMessage(HttpMethod.Get, mediaUrl);
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}).");

        await using var stream = await response.Content.ReadAsStreamAsync();
        var totalLength = response.Content.Headers.ContentLength ?? 0;
        var destination = new MemoryStream();
        await stream.CopyToAsync(destination, (int)totalLength, CancellationToken.None);

        await _objectStorage.Put("audio", id.ToString(), destination, "audio/mpeg");
    }
}