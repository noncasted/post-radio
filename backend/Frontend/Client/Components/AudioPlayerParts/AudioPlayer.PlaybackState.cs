using Microsoft.JSInterop;

namespace Frontend.Client.Components;

public partial class AudioPlayer
{
    private void ResetPlaybackTracking()
    {
        _lastProgressUtc = DateTime.MinValue;
        _bufferingStartedUtc = null;
        _isBuffering = false;
        _hasReceivedProgress = false;
        _loadAndPlayCompletedUtc = null;
        _lastAudioErrorCode = null;
        _lastAudioErrorMessage = null;
        _lastLoadAndPlayError = null;
        _lastCurrentTime = null;
        _lastDuration = null;
        _progressTimeoutResumeAttempts = 0;
        _lastResumeAttemptUtc = null;
    }

    private bool IsCurrent(int generation, string url) => generation == _generation && _audioUrl == url;

    private bool TryGetWatchdogReason(out string reason)
    {
        return AudioPlayerTiming.TryGetWatchdogReason(
            _isBuffering,
            _bufferingStartedUtc,
            _hasReceivedProgress,
            _loadAndPlayCompletedUtc,
            _lastProgressUtc,
            out reason);
    }

    private async Task RecoverAfterLoopFailure()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ComponentToken);
            await SetNext("loop-exception");
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task StopAudio()
    {
        try
        {
            await Js.InvokeVoidAsync("audioHelper.stop");
        }
        catch (JSDisconnectedException)
        {
        }
    }
}