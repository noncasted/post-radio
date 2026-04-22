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
    Task FetchAll(IOperationProgress progress);
    Task Load(PlaylistData playlist, IOperationProgress progress);
    Task LoadAll(IOperationProgress progress);
}

public class PlaylistLoader : IPlaylistLoader
{
    public PlaylistLoader(
        IOrleans orleans,
        SoundCloudClient soundCloud,
        IPlaylistsCollection playlists,
        ISongsCollection songs,
        IMediaStorage mediaStorage,
        HttpClient http,
        ILogger<PlaylistLoader> logger)
    {
        _orleans = orleans;
        _soundCloud = soundCloud;
        _playlists = playlists;
        _songs = songs;
        _mediaStorage = mediaStorage;
        _http = http;
        _logger = logger;
    }

    private readonly HttpClient _http;
    private readonly ILogger<PlaylistLoader> _logger;
    private readonly IMediaStorage _mediaStorage;
    private readonly IOrleans _orleans;
    private readonly IPlaylistsCollection _playlists;
    private readonly ISongsCollection _songs;
    private readonly SoundCloudClient _soundCloud;

    public async Task Fetch(PlaylistData playlist, IOperationProgress progress)
    {
        progress.SetStatus(OperationStatus.InProgress);

        await FetchPlaylist(playlist, progress);

        progress.SetStatus(OperationStatus.Success);
    }

    public async Task FetchAll(IOperationProgress progress)
    {
        progress.SetStatus(OperationStatus.InProgress);
        progress.Log("Scanning all playlists for tracks...");

        var playlists = _playlists
                        .Select(kv => new PlaylistData
                        {
                            Id = kv.Key,
                            Url = kv.Value.Url,
                            Name = kv.Value.Name
                        })
                        .ToList();

        progress.Log($"Found {playlists.Count} playlist(s).");
        progress.SetProgress(0f);

        var fetched = 0;
        var failed = 0;

        for (var i = 0; i < playlists.Count; i++)
        {
            var playlist = playlists[i];
            var playlistName = string.IsNullOrWhiteSpace(playlist.Name) ? playlist.Url : playlist.Name;

            try
            {
                progress.Log($"Fetching {i + 1} / {playlists.Count}: {playlistName}");
                await FetchPlaylist(playlist, progress);
                fetched++;
            }
            catch (Exception e)
            {
                failed++;

                _logger.LogError(e,
                    "[Audio] [Songs] Failed to fetch playlist {PlaylistId} ({PlaylistName})",
                    playlist.Id,
                    playlistName);
                progress.Log($"Failed {i + 1} / {playlists.Count}: {playlistName}: {e.Message}");
            }

            progress.SetProgress((i + 1) / (float)Math.Max(1, playlists.Count));
        }

        progress.Log($"Fetch complete: {fetched} playlist(s), {failed} failed.");
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
        await LoadPending(pending,
            progress,
            (state, e) => _logger.LogError(e,
                "[Audio] [Playlist] Failed to download {Author} - {Name} (playlist {PlaylistId})",
                state.Author, state.Name, playlist.Id));
    }

    public async Task LoadAll(IOperationProgress progress)
    {
        progress.SetStatus(OperationStatus.InProgress);
        progress.Log("Scanning all songs for unloaded audio...");

        var pending = _songs
                      .Where(kv => !kv.Value.IsLoaded)
                      .Select(kv => (Id: kv.Key, State: kv.Value))
                      .ToList();

        progress.Log($"Found {pending.Count} unloaded songs.");
        await LoadPending(pending,
            progress,
            (state, e) => _logger.LogError(e,
                "[Audio] [Songs] Failed to download {Author} - {Name}", state.Author, state.Name));
    }

    private async Task FetchPlaylist(PlaylistData playlist, IOperationProgress progress)
    {
        progress.Log("Fetching playlist tracks...");

        var tracks = new List<SoundCloudExplode.Tracks.Track>();

        await foreach (var track in _soundCloud.Playlists.GetTracksAsync(playlist.Url))
            tracks.Add(track);

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
    }

    private async Task LoadPending(
        IReadOnlyList<(long Id, SongState State)> pending,
        IOperationProgress progress,
        Action<SongState, Exception> logError)
    {
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

                logError(state, e);
                progress.Log($"Failed {i + 1} / {pending.Count}: {e.Message}");
            }

            progress.SetProgress((i + 1) / (float)Math.Max(1, pending.Count));
        }

        progress.Log($"Load complete: {downloaded} downloaded, {failed} failed.");
        progress.SetStatus(OperationStatus.Success);
    }

    private async Task Download(long id, SongState state)
    {
        _logger.LogInformation("[Audio] Downloading {Author} {Name}", state.Author, state.Name);

        var mediaUrl = await _soundCloud.Tracks.GetDownloadUrlAsync(state.Url);

        var request = new HttpRequestMessage(HttpMethod.Get, mediaUrl);
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}).");

        await using var stream = await response.Content.ReadAsStreamAsync();
        await _mediaStorage.SaveAudio(id, stream);
    }
}
