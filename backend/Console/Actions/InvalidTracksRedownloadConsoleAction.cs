using Common;
using Common.Extensions;
using Infrastructure;
using Meta.Audio;
using Microsoft.Extensions.Logging;
using SoundCloudExplode;

namespace Console.Actions;

public class InvalidTracksRedownloadConsoleAction : IConsoleAction
{
    public InvalidTracksRedownloadConsoleAction(
        ISongsCollection songs,
        IPlaylistLoader loader,
        IMediaStorage mediaStorage,
        SoundCloudClient soundCloud,
        IOrleans orleans,
        ILogger<InvalidTracksRedownloadConsoleAction> logger)
    {
        _songs = songs;
        _loader = loader;
        _mediaStorage = mediaStorage;
        _soundCloud = soundCloud;
        _orleans = orleans;
        _logger = logger;
    }

    private static readonly TimeSpan DurationTolerance = TimeSpan.FromSeconds(2);

    private readonly IPlaylistLoader _loader;
    private readonly ILogger<InvalidTracksRedownloadConsoleAction> _logger;
    private readonly IMediaStorage _mediaStorage;
    private readonly IOrleans _orleans;
    private readonly SoundCloudClient _soundCloud;
    private readonly ISongsCollection _songs;

    public string Id => "invalid-tracks-redownload";
    public string Name => "Redownload invalid tracks";
    public string Description => "Scan all SoundCloud tracks, mark invalid duration candidates, redownload them, and report restored/failed results.";

    public async Task Execute(IOperationProgress progress, CancellationToken cancellationToken = default)
    {
        progress.SetStatus(OperationStatus.InProgress);

        var songs = _songs
                      .OrderBy(kv => kv.Value.Author)
                      .ThenBy(kv => kv.Value.Name)
                      .ToList();

        progress.Log($"Scanning {songs.Count} song(s) on SoundCloud.");
        progress.SetProgress(0f);

        var ok = 0;
        var restored = new List<string>();
        var failed = new List<string>();

        for (var i = 0; i < songs.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (id, state) = songs[i];
            var label = FormatLabel(id, state);

            try
            {
                progress.Log($"Scanning {i + 1}/{songs.Count}: {label}");

                var track = await _soundCloud.Tracks.GetAsync(state.Url, cancellationToken);
                var soundCloudDurationMs = PlaylistLoader.GetTrackDurationMs(track);
                var localDuration = state.IsLoaded
                    ? await AudioDurationReader.TryReadDuration(_mediaStorage.GetAudioPath(id), cancellationToken)
                    : null;

                var localDurationMs = PlaylistLoader.ToDurationMs(localDuration);
                var hasInvalidDuration = state.IsLoaded && !AudioTrackValidation.IsValidLocalDuration(localDuration)
                                         || IsInvalidDuration(soundCloudDurationMs);
                var hasDurationMismatch = state.IsLoaded
                                          && soundCloudDurationMs.HasValue
                                          && !IsDurationMatch(localDuration, soundCloudDurationMs.Value);
                var shouldRetry = !state.IsValid || hasInvalidDuration || hasDurationMismatch;

                if (!shouldRetry)
                {
                    ok++;
                    await UpdateStoredAudioData(id, state, localDurationMs, true);
                    continue;
                }

                var reason = FormatReason(state, localDuration, soundCloudDurationMs, hasDurationMismatch);
                progress.Log($"Invalid candidate {label}: {reason}. Marking invalid and redownloading...");

                await MarkInvalid(id, state, localDurationMs);

                var result = await _loader.DownloadSong(id, state, track, cancellationToken);
                var repairedLocalDuration = result.LocalDuration
                                            ?? await AudioDurationReader.TryReadDuration(
                                                _mediaStorage.GetAudioPath(id),
                                                cancellationToken);
                var repairedDurationMs = PlaylistLoader.ToDurationMs(repairedLocalDuration);
                var isValid = AudioTrackValidation.IsValidLocalDuration(repairedLocalDuration)
                              && (!soundCloudDurationMs.HasValue
                                  || IsDurationMatch(repairedLocalDuration, soundCloudDurationMs.Value));

                await _orleans.GetGrain<ISong>(id).SetAudioData(true, repairedDurationMs, isValid);

                if (isValid)
                {
                    restored.Add(label);
                    progress.Log($"Restored {label}: local={FormatDuration(repairedLocalDuration)}, soundcloud={FormatDuration(soundCloudDurationMs)}.");
                }
                else
                {
                    var failedReason = $"redownloaded local={FormatDuration(repairedLocalDuration)}, soundcloud={FormatDuration(soundCloudDurationMs)}";
                    failed.Add($"{label} — {failedReason}");
                    progress.Log($"Failed {label}: {failedReason}. Kept invalid.");
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                failed.Add($"{label} — {e.Message}");
                _logger.LogError(e, "[Audio] [InvalidRetry] Failed to scan/redownload {SongId} {Author} - {Name}", id, state.Author, state.Name);
                await _orleans.GetGrain<ISong>(id).SetValid(false);
                progress.Log($"Failed {label}: {e.Message}. Marked invalid.");
            }
            finally
            {
                progress.SetProgress((i + 1) / (float)Math.Max(1, songs.Count));
            }
        }

        LogResults(progress, "Restored", restored);
        LogResults(progress, "Failed", failed);
        progress.Log($"Complete: scanned={songs.Count}, ok={ok}, restored={restored.Count}, failed={failed.Count}.");
        progress.SetStatus(failed.Count > 0 ? OperationStatus.Failed : OperationStatus.Success);
    }

    private async Task MarkInvalid(long id, SongState state, long? durationMs)
    {
        if (state.IsLoaded)
        {
            await _orleans.GetGrain<ISong>(id).SetAudioData(true, durationMs, false);
            return;
        }

        await _orleans.GetGrain<ISong>(id).SetValid(false);
    }

    private async Task UpdateStoredAudioData(long id, SongState state, long? durationMs, bool isValid)
    {
        if (!state.IsLoaded)
            return;

        if (state.DurationMs == durationMs && state.IsValid == isValid)
            return;

        await _orleans.GetGrain<ISong>(id).SetAudioData(true, durationMs, isValid);
    }

    private static void LogResults(IOperationProgress progress, string title, IReadOnlyList<string> items)
    {
        progress.Log($"{title} ({items.Count}):");

        foreach (var item in items)
            progress.Log($"- {item}");
    }

    private static bool IsInvalidDuration(long? durationMs)
    {
        return durationMs.HasValue && !AudioTrackValidation.IsValidLocalDurationMs(durationMs);
    }

    private static bool IsDurationMatch(TimeSpan? localDuration, long soundCloudDurationMs)
    {
        if (!localDuration.HasValue)
            return false;

        var delta = Math.Abs(localDuration.Value.TotalMilliseconds - soundCloudDurationMs);
        return delta <= DurationTolerance.TotalMilliseconds;
    }

    private static string FormatReason(
        SongState state,
        TimeSpan? localDuration,
        long? soundCloudDurationMs,
        bool hasDurationMismatch)
    {
        var reasons = new List<string>();

        if (!state.IsValid)
            reasons.Add("stored IsValid=false");

        if (state.IsLoaded && !AudioTrackValidation.IsValidLocalDuration(localDuration))
            reasons.Add($"local duration {FormatDuration(localDuration)} is below {FormatDuration(AudioTrackValidation.MinimumPlayableDuration)}");

        if (IsInvalidDuration(soundCloudDurationMs))
            reasons.Add($"SoundCloud duration {FormatDuration(soundCloudDurationMs)} is below {FormatDuration(AudioTrackValidation.MinimumPlayableDuration)}");

        if (hasDurationMismatch)
            reasons.Add($"duration mismatch local={FormatDuration(localDuration)}, soundcloud={FormatDuration(soundCloudDurationMs)}");

        return reasons.Count == 0 ? "requires retry" : string.Join("; ", reasons);
    }

    private static string FormatLabel(long id, SongState state)
    {
        var author = string.IsNullOrWhiteSpace(state.Author) ? "Unknown" : state.Author;
        var name = string.IsNullOrWhiteSpace(state.Name) ? "Untitled" : state.Name;
        return $"{author} — {name} #{id}";
    }

    private static string FormatDuration(TimeSpan duration) => FormatDuration((TimeSpan?)duration);

    private static string FormatDuration(long? durationMs)
    {
        return durationMs.HasValue
            ? FormatDuration(TimeSpan.FromMilliseconds(durationMs.Value))
            : FormatDuration((TimeSpan?)null);
    }

    private static string FormatDuration(TimeSpan? duration)
    {
        if (!duration.HasValue)
            return "missing";

        return duration.Value.TotalHours >= 1
            ? duration.Value.ToString(@"h\:mm\:ss")
            : duration.Value.ToString(@"m\:ss");
    }
}
