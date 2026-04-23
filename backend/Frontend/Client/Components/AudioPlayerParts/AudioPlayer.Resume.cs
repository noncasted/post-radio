using Microsoft.JSInterop;

namespace Frontend.Client.Components;

public partial class AudioPlayer
{
    private async Task<bool> TryResumeBeforeSkip(int generation, string url)
    {
        const int maxAttempts = 2;

        if (_progressTimeoutResumeAttempts >= maxAttempts)
        {
            Logger.LogWarning("[AudioPlayer] Resume skipped attempts={Attempts} generation={Generation}", _progressTimeoutResumeAttempts, generation);
            return false;
        }

        var (invoked, result) = await TryInvokeResume();
        if (!invoked)
            return false;

        _progressTimeoutResumeAttempts++;
        _lastResumeAttemptUtc = DateTime.UtcNow;

        Logger.LogWarning("[AudioPlayer] Resume probe attempt={Attempt} resumed={Resumed} reason={Reason} errorMessage={ErrorMessage} beforeTime={BeforeTime} afterPlayTime={AfterPlayTime} currentTime={CurrentTime} duration={Duration} bufferedEnd={BufferedEnd} readyState={ReadyState} networkState={NetworkState} beforePaused={BeforePaused} afterPlayPaused={AfterPlayPaused} paused={Paused} ended={Ended} seeking={Seeking} nudged={Nudged} nudgeFromTime={NudgeFromTime} nudgeToTime={NudgeToTime} probeElapsedMs={ProbeElapsedMs} hidden={Hidden} visibilityState={VisibilityState}",
            _progressTimeoutResumeAttempts, result?.Resumed, result?.Reason, result?.ErrorMessage,
            result?.BeforeTime, result?.AfterPlayTime, result?.CurrentTime, result?.Duration, result?.BufferedEnd,
            result?.ReadyState, result?.NetworkState, result?.BeforePaused, result?.AfterPlayPaused, result?.Paused,
            result?.Ended, result?.Seeking, result?.Nudged, result?.NudgeFromTime, result?.NudgeToTime,
            result?.ProbeElapsedMs, result?.Hidden, result?.VisibilityState);

        ReportResumeAttempt(generation, result);

        if (result?.Resumed != true || !IsCurrent(generation, url))
            return false;

        _lastProgressUtc = DateTime.UtcNow;
        _bufferingStartedUtc = null;
        _isBuffering = false;
        return true;
    }

    private async Task<(bool Invoked, ResumeResult? Result)> TryInvokeResume()
    {
        try
        {
            return (true, await Js.InvokeAsync<ResumeResult>("audioHelper.tryResumePlayback"));
        }
        catch (JSDisconnectedException)
        {
            return (false, null);
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "[AudioPlayer] Resume invoke failed");
            return (false, null);
        }
    }

    private void ReportResumeAttempt(int generation, ResumeResult? result)
    {
        var payload = new
        {
            reason = "resume-attempt",
            title = "Resume probe before skip",
            severity = result?.Resumed == true ? "info" : "warning",
            resumeProbe = true,
            attempt = _progressTimeoutResumeAttempts,
            resumed = result?.Resumed ?? false,
            detail = result?.Reason,
            errorMessage = result?.ErrorMessage,
            beforeTime = result?.BeforeTime,
            beforePaused = result?.BeforePaused,
            afterPlayTime = result?.AfterPlayTime,
            afterPlayPaused = result?.AfterPlayPaused,
            currentTime = result?.CurrentTime,
            duration = result?.Duration,
            bufferedEnd = result?.BufferedEnd,
            readyState = result?.ReadyState,
            networkState = result?.NetworkState,
            paused = result?.Paused,
            ended = result?.Ended,
            seeking = result?.Seeking,
            nudged = result?.Nudged,
            nudgeFromTime = result?.NudgeFromTime,
            nudgeToTime = result?.NudgeToTime,
            probeElapsedMs = result?.ProbeElapsedMs,
            domHidden = result?.Hidden,
            visibilityState = result?.VisibilityState,
            userAgent = result?.UserAgent,
            songId = State.CurrentSong?.Id,
            songLabel = AudioPlayerFormatters.FormatSongLabel(State.CurrentSong),
            timestampUtc = DateTime.UtcNow,
            sessionId = State.SessionId,
            generation,
            hidden = _isHidden
        };
        _ = PushSkipToBackend(payload);
    }
}