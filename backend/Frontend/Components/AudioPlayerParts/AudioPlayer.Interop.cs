using Microsoft.JSInterop;

namespace Frontend.Components;

public partial class AudioPlayer
{
    /// <summary>Called once per track, right after the browser actually started playing it.</summary>
    [JSInvokable]
    public Task OnTrackStarted(long token, long songId)
    {
        if (_disposed || token <= _barrierToken)
            return Task.CompletedTask;

        var song = TryGetTrack(token);

        Logger.LogInformation("[AudioPlayer] Track started token={Token} songId={SongId} song={SongLabel}",
            token, songId, AudioPlayerFormatters.FormatSongLabel(song));

        State.SetCurrentSong(song);
        return InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Called once per track when JS stops playing it, for any reason. Carries the whole
    /// diagnostic payload collected during playback.
    /// </summary>
    [JSInvokable]
    public async Task OnTrackFinished(long token, long songId, string reason, TrackDiagnostics? diagnostics)
    {
        if (_disposed)
            return;

        var stale = token <= _barrierToken;
        var song = ForgetTrack(token);

        Logger.LogInformation(
            "[AudioPlayer] Track finished token={Token} songId={SongId} reason={Reason} stale={Stale} ct={CurrentTime} bufferingSec={BufferingSec} rs={ReadyState} ns={NetworkState} hidden={Hidden}",
            token, songId, reason, stale, diagnostics?.CurrentTime, diagnostics?.BufferingTotalSeconds,
            diagnostics?.ReadyState, diagnostics?.NetworkState, diagnostics?.Hidden);

        if (stale)
            return;

        ReportTrackFinished(reason, song, diagnostics);
        await EnsureQueue($"finished:{reason}");
    }

    /// <summary>Called when the JS player has nothing left to play and needs the queue topped up.</summary>
    [JSInvokable]
    public async Task OnQueueStarved(string reason)
    {
        if (_disposed)
            return;

        Logger.LogInformation("[AudioPlayer] Queue starved reason={Reason} queued={Queued}", reason, _queuedCount);
        await EnsureQueue($"starved:{reason}");
    }
}
