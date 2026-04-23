using Common.Extensions;
using Infrastructure;
using Meta.Audio;
using Microsoft.Extensions.Logging;

namespace Console.Actions;

public class InvalidTracksRedownloadConsoleAction : IConsoleAction
{
    public InvalidTracksRedownloadConsoleAction(
        ISongsCollection songs,
        IPlaylistLoader loader,
        IOrleans orleans,
        ILogger<InvalidTracksRedownloadConsoleAction> logger)
    {
        _songs = songs;
        _loader = loader;
        _orleans = orleans;
        _logger = logger;
    }

    private readonly IPlaylistLoader _loader;
    private readonly ILogger<InvalidTracksRedownloadConsoleAction> _logger;
    private readonly IOrleans _orleans;
    private readonly ISongsCollection _songs;

    public string Id => "invalid-tracks-redownload";
    public string Name => "Redownload invalid tracks";
    public string Description => "Retry downloading all IsValid=false tracks and report restored/failed results.";

    public async Task Execute(IOperationProgress progress, CancellationToken cancellationToken = default)
    {
        progress.SetStatus(OperationStatus.InProgress);

        var pending = _songs
                      .Where(kv => !kv.Value.IsValid)
                      .OrderBy(kv => kv.Value.Author)
                      .ThenBy(kv => kv.Value.Name)
                      .ToList();

        progress.Log($"Found {pending.Count} invalid song(s).");
        progress.SetProgress(0f);

        var restored = new List<string>();
        var failed = new List<string>();

        for (var i = 0; i < pending.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (id, state) = pending[i];
            var label = FormatLabel(id, state);

            try
            {
                progress.Log($"Retrying {i + 1}/{pending.Count}: {label}");

                var result = await _loader.DownloadSong(id, state, cancellationToken: cancellationToken);
                var isValid = AudioTrackValidation.IsValidLocalDuration(result.LocalDuration);
                await _orleans.GetGrain<ISong>(id).SetAudioData(true, PlaylistLoader.ToDurationMs(result.LocalDuration), isValid);

                if (isValid)
                {
                    restored.Add(label);
                    progress.Log($"Restored {label}: local={FormatDuration(result.LocalDuration)}.");
                }
                else
                {
                    var reason = $"downloaded duration {FormatDuration(result.LocalDuration)} is below {FormatDuration(AudioTrackValidation.MinimumPlayableDuration)}";
                    failed.Add($"{label} — {reason}");
                    progress.Log($"Failed {label}: {reason}. Marked invalid.");
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                failed.Add($"{label} — {e.Message}");
                _logger.LogError(e, "[Audio] [InvalidRetry] Failed to redownload {SongId} {Author} - {Name}", id, state.Author, state.Name);
                await _orleans.GetGrain<ISong>(id).SetValid(false);
                progress.Log($"Failed {label}: {e.Message}. Marked invalid.");
            }
            finally
            {
                progress.SetProgress((i + 1) / (float)Math.Max(1, pending.Count));
            }
        }

        LogResults(progress, "Restored", restored);
        LogResults(progress, "Failed", failed);
        progress.Log($"Complete: restored={restored.Count}, failed={failed.Count}.");
        progress.SetStatus(failed.Count > 0 ? OperationStatus.Failed : OperationStatus.Success);
    }

    private static void LogResults(IOperationProgress progress, string title, IReadOnlyList<string> items)
    {
        progress.Log($"{title} ({items.Count}):");

        foreach (var item in items)
            progress.Log($"- {item}");
    }

    private static string FormatLabel(long id, SongState state)
    {
        var author = string.IsNullOrWhiteSpace(state.Author) ? "Unknown" : state.Author;
        var name = string.IsNullOrWhiteSpace(state.Name) ? "Untitled" : state.Name;
        return $"{author} — {name} #{id}";
    }

    private static string FormatDuration(TimeSpan duration) => FormatDuration((TimeSpan?)duration);

    private static string FormatDuration(TimeSpan? duration)
    {
        if (!duration.HasValue)
            return "missing";

        return duration.Value.TotalHours >= 1
            ? duration.Value.ToString(@"h\:mm\:ss")
            : duration.Value.ToString(@"m\:ss");
    }
}