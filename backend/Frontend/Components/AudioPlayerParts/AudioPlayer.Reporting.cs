using Frontend.Services;
using Frontend.Shared;

namespace Frontend.Components;

public partial class AudioPlayer
{
    private void ReportTrackFinished(string reason, SongDto? song, TrackDiagnostics? diagnostics)
    {
        if (song == null && diagnostics == null)
            return;

        var details = AudioSkipDetailBuilder.Build(song, diagnostics);
        ReportSkip(reason, song, details, diagnostics);
    }

    private void ReportSkip(
        string reason,
        SongDto? previousSong,
        IReadOnlyList<KeyValuePair<string, string?>> details,
        TrackDiagnostics? diagnostics,
        SongDto? candidateSong = null,
        SongStreamUrlResult? streamResult = null,
        string? sourceReason = null)
    {
        var (title, severity) = AudioPlayerSkipPolicy.DescribeReason(reason);
        var songLabel = AudioPlayerFormatters.FormatSongLabel(previousSong);
        var candidateSongLabel = AudioPlayerFormatters.FormatSongLabel(candidateSong);
        var timestamp = DateTime.UtcNow;
        var uiSuppressed = AudioPlayerSkipPolicy.IsUiSuppressed(reason);

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
            audioState = diagnostics,
            details = details.Select(kv => new { key = kv.Key, value = kv.Value }).ToArray(),
            recentSkipCount = State.RecentSkips.Count,
            uiSuppressed
        };

        _ = PushSkipToBackend(payload);
    }

    private void ReportStreamFailure(string sourceReason, SongDto candidateSong, SongStreamUrlResult streamResult)
    {
        var reason = streamResult.IsNotFound
            ? AudioPlayerSkipPolicy.MissingStreamReason
            : AudioPlayerSkipPolicy.StreamUrlFailedReason;

        Logger.LogWarning(
            "[AudioPlayer] Stream url failed sourceReason={SourceReason} songId={SongId} statusCode={StatusCode} notFound={IsNotFound}",
            sourceReason, candidateSong.Id, streamResult.StatusCode, streamResult.IsNotFound);

        var details = AudioSkipDetailBuilder.Build(State.CurrentSong, null)
                                            .Concat(BuildStreamFailureDetails(sourceReason, candidateSong, streamResult))
                                            .ToArray();

        ReportSkip(
            reason,
            State.CurrentSong,
            details,
            diagnostics: null,
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
