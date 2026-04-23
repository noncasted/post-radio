using Common;
using Common.Extensions;
using Infrastructure;
using Meta.Audio;
using Microsoft.Extensions.Logging;
using SoundCloudExplode;
using SoundCloudExplode.Exceptions;

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

    private const int ScanProgressLogInterval = 25;
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

        progress.Log($"Scanning {songs.Count} SoundCloud song record(s).");
        progress.Log("Outcome legend: OK=already valid, CANDIDATE=redownload will be attempted, RESTORED=redownload fixed the track, FAILED=redownload/error kept it invalid, SKIP-WARN=not actionable and skipped.");
        progress.Log("Retry policy: candidates are stored invalid tracks, loaded local files below 0:31, SoundCloud tracks below 0:31, or local/SoundCloud duration mismatches.");
        progress.Log($"Restore policy: redownload is RESTORED only when the saved local file is playable and matches SoundCloud within {FormatDuration(DurationTolerance)}.");
        progress.SetProgress(0f);

        var ok = 0;
        var candidates = 0;
        var markedInvalid = 0;
        var redownloadAttempts = 0;
        var restored = new List<string>();
        var failed = new List<string>();
        var skipped = new List<string>();
        var scanErrors = new List<string>();
        var failureReasons = new Dictionary<string, int>(StringComparer.Ordinal);
        var skipReasons = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < songs.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (id, state) = songs[i];
            var label = SongLogFormatter.FormatLabel(id, state);
            var redownloadStarted = false;

            try
            {
                LogScanProgress(progress, i, songs.Count, ok, candidates, redownloadAttempts, restored.Count, failed.Count, skipped.Count, scanErrors.Count);

                var localDuration = state.IsLoaded
                    ? await AudioDurationReader.TryReadDuration(_mediaStorage.GetAudioPath(id), cancellationToken)
                    : null;
                var localDurationMs = PlaylistLoader.ToDurationMs(localDuration);
                var hasInvalidLocalDuration = state.IsLoaded && !AudioTrackValidation.IsValidLocalDuration(localDuration);
                var hasStoredRetryReason = !state.IsValid || hasInvalidLocalDuration;

                if (string.IsNullOrWhiteSpace(state.Url))
                {
                    await HandleEmptyUrl(
                        id,
                        state,
                        localDuration,
                        localDurationMs,
                        hasStoredRetryReason,
                        skipped,
                        skipReasons,
                        progress);

                    if (hasStoredRetryReason)
                    {
                        candidates++;
                        markedInvalid++;
                    }
                    else
                    {
                        ok++;
                    }

                    continue;
                }

                var track = await _soundCloud.Tracks.GetAsync(state.Url, cancellationToken);
                label = SongLogFormatter.FormatLabel(id, state, track);
                var soundCloudDurationMs = PlaylistLoader.GetTrackDurationMs(track);
                var hasInvalidSoundCloudDuration = IsInvalidDuration(soundCloudDurationMs);
                var hasDurationMismatch = state.IsLoaded
                                          && soundCloudDurationMs.HasValue
                                          && !IsDurationMatch(localDuration, soundCloudDurationMs.Value);
                var shouldRetry = !state.IsValid || hasInvalidLocalDuration || hasInvalidSoundCloudDuration || hasDurationMismatch;

                if (!shouldRetry)
                {
                    ok++;
                    await UpdateStoredAudioData(id, state, localDurationMs, true);
                    continue;
                }

                var reason = FormatReason(state, localDuration, soundCloudDurationMs, hasDurationMismatch);
                candidates++;
                progress.Log($"CANDIDATE {candidates}: {label}; reason={reason}; local={FormatDuration(localDuration)}; soundcloud={FormatDuration(soundCloudDurationMs)}. Marking invalid and redownloading...");

                await MarkInvalid(id, state, localDurationMs);
                markedInvalid++;
                redownloadAttempts++;
                redownloadStarted = true;
                progress.Log($"ATTEMPT {redownloadAttempts}: {label}; requesting SoundCloud progressive stream...");

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
                    restored.Add($"{label} — local={FormatDuration(repairedLocalDuration)}, soundcloud={FormatDuration(soundCloudDurationMs)}");
                    progress.Log($"RESTORED {restored.Count}: {label}; local={FormatDuration(repairedLocalDuration)}; soundcloud={FormatDuration(soundCloudDurationMs)}; marked valid.");
                }
                else
                {
                    var failedReason = $"redownloaded audio is still invalid/mismatched; local={FormatDuration(repairedLocalDuration)}, soundcloud={FormatDuration(soundCloudDurationMs)}";
                    failed.Add($"{label} — {failedReason}");
                    CountReason(failureReasons, "Redownloaded but duration still invalid/mismatched");
                    progress.Log($"FAILED {failed.Count}: {label}; {failedReason}; kept invalid.");
                }
            }
            catch (TrackUnavailableException e)
            {
                var normalizedReason = NormalizeFailureReason(e.Message);
                CountReason(failureReasons, "SoundCloud has no usable progressive stream");
                _logger.LogWarning(e, "[Audio] [InvalidRetry] SoundCloud stream unavailable for {SongId} {Label} redownloadStarted={RedownloadStarted}", id, label, redownloadStarted);
                await _orleans.GetGrain<ISong>(id).SetValid(false);

                if (redownloadStarted)
                {
                    failed.Add($"{label} — SoundCloud has no usable progressive stream: {normalizedReason}");
                    progress.Log($"FAILED {failed.Count}: {label}; SoundCloud has no usable progressive stream ({normalizedReason}). Marked invalid.");
                }
                else
                {
                    scanErrors.Add($"{label} — SoundCloud has no usable progressive stream: {normalizedReason}");
                    progress.Log($"FAILED-SCAN {scanErrors.Count}: {label}; SoundCloud has no usable progressive stream ({normalizedReason}). Marked invalid.");
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                var normalizedReason = NormalizeFailureReason(e.Message);
                CountReason(failureReasons, normalizedReason);
                _logger.LogError(e, "[Audio] [InvalidRetry] Failed to scan/redownload {SongId} {Label} redownloadStarted={RedownloadStarted}", id, label, redownloadStarted);
                await _orleans.GetGrain<ISong>(id).SetValid(false);

                if (redownloadStarted)
                {
                    failed.Add($"{label} — {normalizedReason}");
                    progress.Log($"FAILED {failed.Count}: {label}; redownload error={normalizedReason}. Marked invalid.");
                }
                else
                {
                    scanErrors.Add($"{label} — {normalizedReason}");
                    progress.Log($"FAILED-SCAN {scanErrors.Count}: {label}; scan error={normalizedReason}. Marked invalid.");
                }
            }
            finally
            {
                progress.SetProgress((i + 1) / (float)Math.Max(1, songs.Count));
            }
        }

        progress.Log("Final result sections below are grouped by outcome; counts answer whether anything was actually redownloaded.");
        LogReasons(progress, "Skip warnings", skipReasons);
        LogReasons(progress, "Failure reasons", failureReasons);
        LogResults(progress, "RESTORED redownloads", restored);
        LogResults(progress, "FAILED redownloads", failed);
        LogResults(progress, "FAILED scans", scanErrors);
        LogResults(progress, "SKIP-WARN skipped without redownload", skipped);
        progress.Log(
            $"Complete: scanned={songs.Count}, ok={ok}, candidates={candidates}, markedInvalid={markedInvalid}, redownloadAttempts={redownloadAttempts}, restored={restored.Count}, failedRedownloads={failed.Count}, scanErrors={scanErrors.Count}, skippedWarnings={skipped.Count}.");

        if (redownloadAttempts == 0)
            progress.Log("Result: no redownload was attempted. Check SKIP-WARN / FAILED-SCAN sections for blockers such as empty-url or SoundCloud metadata errors.");
        else if (restored.Count == 0)
            progress.Log("Result: redownloads were attempted, but no invalid candidate was restored. Check Failure reasons and FAILED redownloads above.");

        progress.SetStatus(failed.Count > 0 || scanErrors.Count > 0 ? OperationStatus.Failed : OperationStatus.Success);
    }

    private async Task HandleEmptyUrl(
        long id,
        SongState state,
        TimeSpan? localDuration,
        long? localDurationMs,
        bool hasStoredRetryReason,
        List<string> skipped,
        Dictionary<string, int> skipReasons,
        IOperationProgress progress)
    {
        var label = SongLogFormatter.FormatLabel(id, state);

        if (!hasStoredRetryReason)
        {
            await UpdateStoredAudioData(id, state, localDurationMs, true);
            return;
        }

        var reason = "empty-url: stored SoundCloud URL is empty; cannot fetch metadata or redownload";
        await MarkInvalid(id, state, localDurationMs);
        CountReason(skipReasons, "empty-url (missing SoundCloud URL)");
        skipped.Add($"{label} — {reason}; local={FormatDuration(localDuration)}; marked invalid; no redownload attempted");
        progress.Log($"SKIP-WARN {skipped.Count}: {label}; {reason}; local={FormatDuration(localDuration)}. Marked invalid; no redownload attempted.");
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

    private static void LogScanProgress(
        IOperationProgress progress,
        int index,
        int total,
        int ok,
        int candidates,
        int redownloadAttempts,
        int restored,
        int failed,
        int skipped,
        int scanErrors)
    {
        if (index == 0 || (index + 1) % ScanProgressLogInterval == 0 || index + 1 == total)
        {
            progress.Log(
                $"Scan progress: {index + 1}/{total}; ok={ok}, candidates={candidates}, attempts={redownloadAttempts}, restored={restored}, failedRedownloads={failed}, scanErrors={scanErrors}, skippedWarnings={skipped}.");
        }
    }

    private static void LogResults(IOperationProgress progress, string title, IReadOnlyList<string> items)
    {
        progress.Log($"{title} ({items.Count}):");

        if (items.Count == 0)
        {
            progress.Log("- none");
            return;
        }

        foreach (var item in items)
            progress.Log($"- {item}");
    }

    private static void LogReasons(IOperationProgress progress, string title, IReadOnlyDictionary<string, int> reasons)
    {
        progress.Log($"{title} ({reasons.Count}):");

        if (reasons.Count == 0)
        {
            progress.Log("- none");
            return;
        }

        foreach (var (reason, count) in reasons.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key))
            progress.Log($"- {reason} — {count}");
    }

    private static void CountReason(IDictionary<string, int> reasons, string reason)
    {
        if (reasons.TryGetValue(reason, out var count))
        {
            reasons[reason] = count + 1;
            return;
        }

        reasons[reason] = 1;
    }

    private static string NormalizeFailureReason(string message)
    {
        var diagnosticStart = message.IndexOf(" (trackDuration=", StringComparison.Ordinal);
        return diagnosticStart > 0
            ? message[..diagnosticStart]
            : message;
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
