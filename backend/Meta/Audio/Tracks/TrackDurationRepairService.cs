using Common;
using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Logging;
using SoundCloudExplode;
using SoundCloudExplode.Tracks;

namespace Meta.Audio;

public interface ITrackDurationRepairService
{
    Task Run(IOperationProgress progress, CancellationToken cancellationToken = default);
}

public class TrackDurationRepairService : ITrackDurationRepairService
{
    public TrackDurationRepairService(
        ISongsCollection songs,
        IMediaStorage mediaStorage,
        SoundCloudClient soundCloud,
        IPlaylistLoader loader,
        IOrleans orleans,
        ILogger<TrackDurationRepairService> logger)
    {
        _songs = songs;
        _mediaStorage = mediaStorage;
        _soundCloud = soundCloud;
        _loader = loader;
        _orleans = orleans;
        _logger = logger;
    }

    private static readonly TimeSpan DurationTolerance = TimeSpan.FromSeconds(2);

    private readonly IPlaylistLoader _loader;
    private readonly ILogger<TrackDurationRepairService> _logger;
    private readonly IMediaStorage _mediaStorage;
    private readonly IOrleans _orleans;
    private readonly ISongsCollection _songs;
    private readonly SoundCloudClient _soundCloud;

    public async Task Run(IOperationProgress progress, CancellationToken cancellationToken = default)
    {
        progress.SetStatus(OperationStatus.InProgress);

        var songs = _songs
                    .Where(kv => kv.Value.IsLoaded)
                    .OrderBy(kv => kv.Value.Author)
                    .ThenBy(kv => kv.Value.Name)
                    .ToList();

        progress.Log($"Checking {songs.Count} loaded song(s).");
        progress.SetProgress(0f);

        var checkedCount = 0;
        var okCount = 0;
        var repairedCount = 0;
        var failedCount = 0;
        var skippedCount = 0;

        for (var i = 0; i < songs.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (id, state) = songs[i];
            var label = FormatLabel(id, state);

            try
            {
                progress.Log($"Checking {i + 1}/{songs.Count}: {label}");

                var localDuration = await AudioDurationReader.TryReadDuration(
                    _mediaStorage.GetAudioPath(id),
                    cancellationToken);
                var localDurationMs = PlaylistLoader.ToDurationMs(localDuration);
                var localIsValid = AudioTrackValidation.IsValidLocalDuration(localDuration);
                await UpdateStoredAudioData(id, state, localDurationMs, localIsValid);

                if (!localIsValid)
                {
                    progress.Log(
                        $"ERROR: invalid local audio for {label}: local={FormatDuration(localDuration)}, minimum={FormatDuration(AudioTrackValidation.MinimumPlayableDuration)}.");
                }

                var track = await _soundCloud.Tracks.GetAsync(state.Url, cancellationToken);
                var soundCloudDurationMs = PlaylistLoader.GetTrackDurationMs(track);
                if (!soundCloudDurationMs.HasValue)
                {
                    skippedCount++;
                    progress.Log($"WARN: SoundCloud duration is unavailable for {label}.");
                    continue;
                }

                if (IsDurationMatch(localDuration, soundCloudDurationMs.Value))
                {
                    okCount++;
                    checkedCount++;
                    continue;
                }

                _logger.LogError(
                    "[Audio] [DurationAudit] Duration mismatch for {SongId} {Author} - {Name}: local={LocalDuration} soundCloud={SoundCloudDurationMs}ms",
                    id,
                    state.Author,
                    state.Name,
                    localDuration,
                    soundCloudDurationMs);

                progress.Log(
                    $"ERROR: duration mismatch for {label}: local={FormatDuration(localDuration)}, soundcloud={FormatDuration(soundCloudDurationMs.Value)}. Redownloading...");

                var download = await _loader.DownloadSong(id, state, track, cancellationToken);

                var repairedLocalDuration = download.LocalDuration
                                            ?? await AudioDurationReader.TryReadDuration(_mediaStorage.GetAudioPath(id), cancellationToken);
                var repairedIsValid = AudioTrackValidation.IsValidLocalDuration(repairedLocalDuration);
                await _orleans.GetGrain<ISong>(id).SetAudioData(true, PlaylistLoader.ToDurationMs(repairedLocalDuration), repairedIsValid);

                if (repairedIsValid && IsDurationMatch(repairedLocalDuration, soundCloudDurationMs.Value))
                {
                    repairedCount++;
                    checkedCount++;
                    progress.Log($"Repaired {label}: local={FormatDuration(repairedLocalDuration)}.");
                }
                else
                {
                    failedCount++;
                    progress.Log(
                        $"ERROR: redownload did not fix {label}: local={FormatDuration(repairedLocalDuration)}, soundcloud={FormatDuration(soundCloudDurationMs.Value)}.");
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                failedCount++;
                _logger.LogError(e, "[Audio] [DurationAudit] Failed to check {SongId} {Author} - {Name}", id, state.Author, state.Name);
                await _orleans.GetGrain<ISong>(id).SetValid(false);
                progress.Log($"ERROR: failed to check {label}: {e.Message}");
            }
            finally
            {
                progress.SetProgress((i + 1) / (float)Math.Max(1, songs.Count));
            }
        }

        progress.Log(
            $"Complete: checked={checkedCount}, ok={okCount}, repaired={repairedCount}, skipped={skippedCount}, failed={failedCount}.");
        progress.SetStatus(failedCount > 0 ? OperationStatus.Failed : OperationStatus.Success);
    }

    private async Task UpdateStoredAudioData(long id, SongState state, long? durationMs, bool isValid)
    {
        if (state.DurationMs == durationMs && state.IsValid == isValid)
            return;

        await _orleans.GetGrain<ISong>(id).SetAudioData(state.IsLoaded, durationMs, isValid);
    }

    private static bool IsDurationMatch(TimeSpan? localDuration, long soundCloudDurationMs)
    {
        if (!localDuration.HasValue)
            return false;

        var delta = Math.Abs(localDuration.Value.TotalMilliseconds - soundCloudDurationMs);
        return delta <= DurationTolerance.TotalMilliseconds;
    }

    private static string FormatLabel(long id, SongState state)
    {
        var author = string.IsNullOrWhiteSpace(state.Author) ? "Unknown" : state.Author;
        var name = string.IsNullOrWhiteSpace(state.Name) ? "Untitled" : state.Name;
        return $"{author} — {name} #{id}";
    }

    private static string FormatDuration(long durationMs) => FormatDuration(TimeSpan.FromMilliseconds(durationMs));

    private static string FormatDuration(TimeSpan? duration)
    {
        if (!duration.HasValue)
            return "missing";

        return duration.Value.TotalHours >= 1
            ? duration.Value.ToString(@"h\:mm\:ss")
            : duration.Value.ToString(@"m\:ss");
    }
}
