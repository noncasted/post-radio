using Frontend.Client.Services;
using Frontend.Shared;
using Microsoft.JSInterop;

namespace Frontend.Client.Components;

public partial class AudioPlayer
{
    private async Task<AudioStateSnapshot?> TryCaptureSnapshot()
    {
        try
        {
            return await Js.InvokeAsync<AudioStateSnapshot?>("audioHelper.getState");
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
        catch (Exception e)
        {
            Logger.LogDebug(e, "[AudioPlayer] getState failed");
            return null;
        }
    }

    private bool IsUiSuppressed(string reason)
    {
        return AudioPlayerSkipPolicy.IsUiSuppressed(reason, _componentStartUtc);
    }

    private void ReportSkip(
        string reason,
        SongDto? previousSong,
        IReadOnlyList<KeyValuePair<string, string?>> details,
        AudioStateSnapshot? snapshot,
        SongDto? candidateSong = null,
        SongStreamUrlResult? streamResult = null,
        string? sourceReason = null)
    {
        var (title, severity) = AudioPlayerSkipPolicy.DescribeReason(reason);
        var songLabel = AudioPlayerFormatters.FormatSongLabel(previousSong);
        var candidateSongLabel = AudioPlayerFormatters.FormatSongLabel(candidateSong);
        var timestamp = DateTime.UtcNow;
        var uiSuppressed = IsUiSuppressed(reason);

        Logger.LogInformation(
            "[AudioPlayer] ReportSkip reason={Reason} sourceReason={SourceReason} severity={Severity} songId={SongId} songLabel={SongLabel} candidateSongId={CandidateSongId} candidateSongLabel={CandidateSongLabel} streamStatus={StreamStatusCode} streamNotFound={StreamIsNotFound} uiSuppressed={UiSuppressed} details=[{Details}]",
            reason, sourceReason, severity, previousSong?.Id, songLabel, candidateSong?.Id, candidateSongLabel,
            streamResult?.StatusCode, streamResult?.IsNotFound, uiSuppressed,
            string.Join(", ", details.Select(kv => $"{kv.Key}={kv.Value ?? "-"}")));

        if (!uiSuppressed)
        {
            var notification = new SkipNotification(
                Reason: reason,
                Title: title,
                Severity: severity,
                SongLabel: songLabel,
                TimestampUtc: timestamp,
                Details: details);

            State.ReportSkip(notification);
        }

        var payload = new
        {
            reason,
            title,
            severity,
            songId = previousSong?.Id,
            songLabel,
            candidateSongId = candidateSong?.Id,
            candidateSongLabel,
            sourceReason,
            streamStatusCode = streamResult?.StatusCode,
            streamIsNotFound = streamResult?.IsNotFound,
            timestampUtc = timestamp,
            sessionId = State.SessionId,
            generation = _generation,
            audioState = snapshot,
            details = details.Select(kv => new { key = kv.Key, value = kv.Value }).ToArray(),
            recentSkipCount = State.RecentSkips.Count,
            uiSuppressed
        };

        _ = PushSkipToBackend(payload);
    }

    private async Task ReportStreamFailure(string sourceReason, SongDto candidateSong, SongStreamUrlResult streamResult)
    {
        var reason = streamResult.IsNotFound
            ? AudioPlayerSkipPolicy.MissingStreamReason
            : AudioPlayerSkipPolicy.StreamUrlFailedReason;
        var snapshot = await TryCaptureSnapshot();
        var details = BuildDetails(snapshot, State.CurrentSong)
                      .Concat(BuildStreamFailureDetails(sourceReason, candidateSong, streamResult))
                      .ToArray();

        ReportSkip(
            reason,
            State.CurrentSong,
            details,
            snapshot,
            candidateSong,
            streamResult,
            sourceReason);
    }

    private static IReadOnlyList<KeyValuePair<string, string?>> BuildStreamFailureDetails(
        string sourceReason,
        SongDto candidateSong,
        SongStreamUrlResult streamResult)
    {
        return new[]
        {
            new KeyValuePair<string, string?>("sourceReason", sourceReason),
            new KeyValuePair<string, string?>("candidateSongId", candidateSong.Id.ToString()),
            new KeyValuePair<string, string?>("candidateSong", AudioPlayerFormatters.FormatSongLabel(candidateSong)),
            new KeyValuePair<string, string?>("streamStatus", streamResult.StatusCode?.ToString()),
            new KeyValuePair<string, string?>("streamNotFound", streamResult.IsNotFound.ToString())
        };
    }

    private async Task PushSkipToBackend(object payload)
    {
        try
        {
            await Api.ReportSkip(payload, ComponentToken);
        }
        catch (Exception e)
        {
            Logger.LogDebug(e, "[AudioPlayer] PushSkipToBackend failed");
        }
    }
}
