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
        var toUpdate = new List<SongData>();

        foreach (var track in tracks)
        {
            var author = track.PublisherMetadata is { Artist: not null }
                ? track.PublisherMetadata.Artist
                : string.Empty;
            var name = track.Title ?? string.Empty;
            var url = track.PermalinkUrl?.ToString() ?? string.Empty;
            var localDuration = await ReadLocalDuration(track.Id);
            var localDurationMs = ToDurationMs(localDuration);

            if (_songs.TryGetValue(track.Id, out var existing))
            {
                var playlists = existing.Playlists.Contains(playlist.Id)
                    ? existing.Playlists
                    : existing.Playlists.Concat(new[] { playlist.Id }).ToList();
                var addDate = existing.Playlists.Contains(playlist.Id)
                    ? existing.AddDate
                    : DateTime.UtcNow;
                var isValid = GetFetchValidity(author, existing, localDuration);

                if (existing.Url != url
                    || existing.Author != author
                    || existing.Name != name
                    || !existing.Playlists.SequenceEqual(playlists)
                    || existing.AddDate != addDate
                    || existing.DurationMs != localDurationMs
                    || existing.IsValid != isValid)
                {
                    toUpdate.Add(new SongData
                    {
                        Id = track.Id,
                        Url = url,
                        Playlists = playlists,
                        Author = author,
                        Name = name,
                        AddDate = addDate,
                        IsLoaded = existing.IsLoaded,
                        DurationMs = localDurationMs,
                        IsValid = isValid
                    });
                }

                continue;
            }

            toCreate.Add(new SongData
            {
                Id = track.Id,
                Url = url,
                Playlists = new[] { playlist.Id },
                Author = author,
                Name = name,
                AddDate = DateTime.UtcNow,
                IsLoaded = false,
                DurationMs = localDurationMs,
                IsValid = IsValidAuthor(author)
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

        progress.Log($"Updating {toUpdate.Count} existing song grains...");
        progress.SetProgress(0f);

        for (var i = 0; i < toUpdate.Count; i++)
        {
            var data = toUpdate[i];
            var grain = _orleans.GetGrain<ISong>(data.Id);
            await grain.UpdateData(data);

            progress.SetProgress(i / (float)Math.Max(1, toUpdate.Count));
            progress.Log($"Updated {i + 1} / {toUpdate.Count}: {data.Author} - {data.Name} (valid={data.IsValid})");
        }

        progress.Log($"Fetch complete: {toCreate.Count} new, {toUpdate.Count} updated.");
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

        _logger.LogInformation(
            "[Audio] Download source for {SongId} {Author} - {Name}: {TranscodingAvailability}",
            id,
            state.Author,
            state.Name,
            FormatTranscodingAvailability(track));

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

    private static bool GetFetchValidity(string author, SongState state, TimeSpan? localDuration)
    {
        if (!IsValidAuthor(author))
            return false;

        if (localDuration.HasValue)
            return AudioTrackValidation.IsValidLocalDuration(localDuration);

        return !state.IsLoaded && state.IsValid;
    }

    private static bool IsValidAuthor(string author)
    {
        return !string.IsNullOrWhiteSpace(author);
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
            throw new TrackUnavailableException(
                $"No non-snipped progressive transcodings found ({FormatTranscodingAvailability(track)})");

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

    private static string FormatTranscodingAvailability(Track track)
    {
        var expectedDurationMs = GetTrackDurationMs(track);
        var transcodings = track.Media?.Transcodings?.ToList() ?? [];

        if (transcodings.Count == 0)
            return $"trackDuration={FormatDuration(expectedDurationMs)}, total=0";

        var progressive = transcodings.Count(IsProgressive);
        var hls = transcodings.Count(IsHls);
        var snipped = transcodings.Count(transcoding => transcoding.Snipped);
        var missingUrl = transcodings.Count(transcoding => transcoding.Url == null);
        var durationMismatch = transcodings.Count(transcoding => HasTranscodingDurationMismatch(transcoding, expectedDurationMs));
        var usable = transcodings.Count(transcoding => IsUsableTranscoding(transcoding, expectedDurationMs));
        var sample = string.Join(", ", transcodings.Take(5).Select(FormatTranscoding));

        return $"trackDuration={FormatDuration(expectedDurationMs)}, total={transcodings.Count}, progressive={progressive}, hls={hls}, snipped={snipped}, missingUrl={missingUrl}, durationMismatch={durationMismatch}, usable={usable}, sample=[{sample}]";
    }

    private static bool HasTranscodingDurationMismatch(Transcoding transcoding, long? expectedDurationMs)
    {
        return expectedDurationMs.HasValue
               && transcoding.Duration.HasValue
               && Math.Abs(transcoding.Duration.Value - expectedDurationMs.Value) > DurationTolerance.TotalMilliseconds;
    }

    private static string FormatTranscoding(Transcoding transcoding)
    {
        var protocol = string.IsNullOrWhiteSpace(transcoding.Format?.Protocol)
            ? "unknown"
            : transcoding.Format.Protocol;
        var mime = string.IsNullOrWhiteSpace(transcoding.Format?.MimeType)
            ? "unknown"
            : transcoding.Format.MimeType;
        var quality = string.IsNullOrWhiteSpace(transcoding.Quality)
            ? "unknown"
            : transcoding.Quality;
        var url = transcoding.Url == null ? "missing-url" : "has-url";

        return $"{protocol}/{mime}/{quality}/duration={FormatDuration(transcoding.Duration)}/snipped={transcoding.Snipped}/{url}";
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

    private static bool IsHls(Transcoding transcoding)
    {
        return string.Equals(transcoding.Format?.Protocol, "hls", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatDuration(long? durationMs)
    {
        return durationMs.HasValue
            ? FormatDuration(TimeSpan.FromMilliseconds(durationMs.Value))
            : FormatDuration((TimeSpan?)null);
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
