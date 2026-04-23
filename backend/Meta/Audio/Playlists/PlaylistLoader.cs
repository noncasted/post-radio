using System.Text.Json;
using Common;
using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Logging;
using SoundCloudExplode;
using SoundCloudExplode.Exceptions;
using SoundCloudExplode.Tracks;

namespace Meta.Audio;

public interface IPlaylistLoader
{
    Task Fetch(PlaylistData playlist, IOperationProgress progress);
    Task FetchAll(IOperationProgress progress);
    Task Load(PlaylistData playlist, IOperationProgress progress);
    Task LoadAll(IOperationProgress progress);
    Task<SongDownloadResult> DownloadSong(long id, SongState state, Track? track = null, CancellationToken cancellationToken = default);
}

public sealed record SongDownloadResult(long? SoundCloudDurationMs, TimeSpan? LocalDuration);

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

    private static readonly TimeSpan DurationTolerance = TimeSpan.FromSeconds(2);

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
                      .Where(kv => kv.Value.Playlists.Contains(playlist.Id) && !kv.Value.IsLoaded && kv.Value.IsValid)
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
                      .Where(kv => !kv.Value.IsLoaded && kv.Value.IsValid)
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
        var toUpdateAudio = new List<(long Id, SongState State, long? DurationMs, bool IsValid)>();

        foreach (var track in tracks)
        {
            var localDuration = await ReadLocalDuration(track.Id);
            var localDurationMs = ToDurationMs(localDuration);

            if (_songs.TryGetValue(track.Id, out var existing))
            {
                if (!existing.Playlists.Contains(playlist.Id))
                    toAttach.Add(track.Id);

                var isValid = GetFetchValidity(existing, localDuration);
                if (existing.DurationMs != localDurationMs || existing.IsValid != isValid)
                    toUpdateAudio.Add((track.Id, existing, localDurationMs, isValid));

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
                IsLoaded = false,
                DurationMs = localDurationMs,
                IsValid = true
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

        progress.Log($"Updating local audio data for {toUpdateAudio.Count} existing songs...");
        progress.SetProgress(0f);

        for (var i = 0; i < toUpdateAudio.Count; i++)
        {
            var (id, state, durationMs, isValid) = toUpdateAudio[i];
            await _orleans.GetGrain<ISong>(id).SetAudioData(state.IsLoaded, durationMs, isValid);

            progress.SetProgress(i / (float)Math.Max(1, toUpdateAudio.Count));
            progress.Log($"Updated local audio {i + 1} / {toUpdateAudio.Count}: {state.Author} - {state.Name}");
        }

        progress.Log($"Fetch complete: {toCreate.Count} new, {toAttach.Count} attached, {toUpdateAudio.Count} local audio updated.");
    }

    private async Task LoadPending(
        IReadOnlyList<(long Id, SongState State)> pending,
        IOperationProgress progress,
        Action<SongState, Exception> logError)
    {
        progress.SetProgress(0f);

        var downloaded = 0;
        var invalid = 0;
        var failed = 0;

        for (var i = 0; i < pending.Count; i++)
        {
            var (id, state) = pending[i];

            try
            {
                var result = await DownloadSong(id, state);
                var isValid = AudioTrackValidation.IsValidLocalDuration(result.LocalDuration);
                await _orleans.GetGrain<ISong>(id).SetAudioData(true, ToDurationMs(result.LocalDuration), isValid);
                if (!isValid)
                {
                    invalid++;
                    progress.Log($"Invalid short audio {i + 1} / {pending.Count}: {state.Author} - {state.Name} ({FormatDuration(result.LocalDuration)}). Marked invalid.");
                }
                else
                {
                    downloaded++;
                    progress.Log($"Loaded {i + 1} / {pending.Count}: {state.Author} - {state.Name}");
                }
            }
            catch (TrackUnavailableException)
            {
                failed++;
                await _orleans.GetGrain<ISong>(id).SetValid(false);
                progress.Log($"Unavailable {i + 1} / {pending.Count}: {state.Author} - {state.Name}. Marked invalid.");
            }
            catch (Exception e)
            {
                failed++;

                logError(state, e);
                await _orleans.GetGrain<ISong>(id).SetValid(false);
                progress.Log($"Failed {i + 1} / {pending.Count}: {e.Message}. Marked invalid.");
            }

            progress.SetProgress((i + 1) / (float)Math.Max(1, pending.Count));
        }

        progress.Log($"Load complete: {downloaded} downloaded, {invalid} invalid, {failed} failed.");
        progress.SetStatus(OperationStatus.Success);
    }

    public async Task<SongDownloadResult> DownloadSong(
        long id,
        SongState state,
        Track? track = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Audio] Downloading {Author} {Name}", state.Author, state.Name);

        track ??= await _soundCloud.Tracks.GetAsync(state.Url, cancellationToken)
                  ?? throw new InvalidOperationException($"SoundCloud track not found: {state.Url}");

        var mediaUrl = await GetDownloadUrlAsync(track, cancellationToken);
        if (string.IsNullOrWhiteSpace(mediaUrl))
            throw new InvalidOperationException($"SoundCloud download URL not found: {state.Url}");

        using var request = new HttpRequestMessage(HttpMethod.Get, mediaUrl);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}).");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await _mediaStorage.SaveAudio(id, stream, cancellationToken);

        var localDuration = await AudioDurationReader.TryReadDuration(_mediaStorage.GetAudioPath(id), cancellationToken);
        return new SongDownloadResult(GetTrackDurationMs(track), localDuration);
    }

    private async Task<TimeSpan?> ReadLocalDuration(long id, CancellationToken cancellationToken = default)
    {
        return await AudioDurationReader.TryReadDuration(_mediaStorage.GetAudioPath(id), cancellationToken);
    }

    public static long? ToDurationMs(TimeSpan? duration)
    {
        return duration.HasValue
            ? (long)Math.Round(duration.Value.TotalMilliseconds)
            : null;
    }

    private static bool GetFetchValidity(SongState state, TimeSpan? localDuration)
    {
        if (localDuration.HasValue)
            return AudioTrackValidation.IsValidLocalDuration(localDuration);

        return !state.IsLoaded && state.IsValid;
    }

    private static string FormatDuration(TimeSpan? duration)
    {
        if (!duration.HasValue)
            return "missing";

        return duration.Value.TotalHours >= 1
            ? duration.Value.ToString(@"h\:mm\:ss")
            : duration.Value.ToString(@"m\:ss");
    }

    private async Task<string?> GetDownloadUrlAsync(Track track, CancellationToken cancellationToken)
    {
        var transcoding = SelectProgressiveTranscoding(track);
        if (transcoding == null)
            throw new TrackUnavailableException("No non-snipped progressive transcodings found");

        if (transcoding.Url == null)
            return null;

        var uri = new UriBuilder(transcoding.Url)
        {
            Query = "client_id=" + Uri.EscapeDataString(_soundCloud.ClientId)
        }.Uri;

        using var response = await _http.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}).");

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("url", out var urlElement))
            return null;

        var url = urlElement.GetString();
        if (url?.Contains(".m3u8", StringComparison.OrdinalIgnoreCase) == true)
            throw new TrackUnavailableException("Selected SoundCloud transcoding is HLS, not a direct audio file");

        return url;
    }

    private static Transcoding? SelectProgressiveTranscoding(Track track)
    {
        var expectedDurationMs = GetTrackDurationMs(track);
        var transcodings = track.Media?.Transcodings
                           ?.Where(transcoding => IsUsableTranscoding(transcoding, expectedDurationMs))
                           .ToList();

        if (transcodings == null || transcodings.Count == 0)
            return null;

        return transcodings.FirstOrDefault(IsSqMp3Progressive)
               ?? transcodings.FirstOrDefault(IsMp3Progressive)
               ?? transcodings.FirstOrDefault(IsProgressive);
    }

    private static bool IsUsableTranscoding(Transcoding transcoding, long? expectedDurationMs)
    {
        if (transcoding.Snipped || transcoding.Url == null || !IsProgressive(transcoding))
            return false;

        return !expectedDurationMs.HasValue
               || !transcoding.Duration.HasValue
               || Math.Abs(transcoding.Duration.Value - expectedDurationMs.Value) <= DurationTolerance.TotalMilliseconds;
    }

    private static bool IsSqMp3Progressive(Transcoding transcoding)
    {
        return string.Equals(transcoding.Quality, "sq", StringComparison.OrdinalIgnoreCase)
               && IsMp3Progressive(transcoding);
    }

    private static bool IsMp3Progressive(Transcoding transcoding)
    {
        return IsProgressive(transcoding)
               && transcoding.Format?.MimeType?.Contains("audio/mpeg", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsProgressive(Transcoding transcoding)
    {
        return string.Equals(transcoding.Format?.Protocol, "progressive", StringComparison.OrdinalIgnoreCase);
    }

    public static long? GetTrackDurationMs(Track? track)
    {
        if (track == null)
            return null;

        return track.FullDuration is > 0
            ? track.FullDuration
            : track.Duration is > 0
                ? track.Duration
                : null;
    }
}
