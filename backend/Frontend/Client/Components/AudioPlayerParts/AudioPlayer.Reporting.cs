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
        AudioStateSnapshot? snapshot)
    {
        var (title, severity) = AudioPlayerSkipPolicy.DescribeReason(reason);
        var songLabel = AudioPlayerFormatters.FormatSongLabel(previousSong);
        var timestamp = DateTime.UtcNow;
        var uiSuppressed = IsUiSuppressed(reason);

        Logger.LogInformation(
            "[AudioPlayer] ReportSkip reason={Reason} severity={Severity} songId={SongId} songLabel={SongLabel} uiSuppressed={UiSuppressed} details=[{Details}]",
            reason, severity, previousSong?.Id, songLabel, uiSuppressed,
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