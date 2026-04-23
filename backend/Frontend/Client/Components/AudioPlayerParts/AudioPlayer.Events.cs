using System.Globalization;
using Microsoft.JSInterop;

namespace Frontend.Client.Components;

public partial class AudioPlayer
{
    [JSInvokable]
    public Task OnVisibilityChange(bool hidden, string state)
    {
        if (_disposed)
            return Task.CompletedTask;

        var now = DateTime.UtcNow;
        var previouslyHidden = _isHidden;
        _isHidden = hidden;

        if (hidden)
        {
            _hiddenSinceUtc ??= now;
            Logger.LogInformation("[AudioPlayer] Visibility hidden state={State} generation={Generation}", state, _generation);
        }
        else
        {
            var hiddenFor = _hiddenSinceUtc.HasValue ? (now - _hiddenSinceUtc.Value).TotalSeconds : 0;
            _hiddenSinceUtc = null;

            if (previouslyHidden)
            {
                _lastProgressUtc = now;
                _bufferingStartedUtc = _isBuffering ? now : null;
                _progressTimeoutResumeAttempts = 0;
            }

            Logger.LogInformation("[AudioPlayer] Visibility visible state={State} generation={Generation} hiddenSec={HiddenSec:F1}", state, _generation, hiddenFor);
        }

        return Task.CompletedTask;
    }

    [JSInvokable]
    public async Task OnAudioEvent(
        string eventName,
        int generation,
        double? currentTime,
        double? duration,
        int readyState,
        int networkState,
        bool paused,
        bool ended,
        double? bufferedEnd,
        int? errorCode,
        string? errorMessage)
    {
        if (_disposed)
            return;

        CaptureAudioEventState(currentTime, duration, readyState, networkState, errorCode, errorMessage);

        if (generation != _generation)
        {
            Logger.LogDebug("[AudioPlayer] Stale event event={EventName} eventGeneration={EventGeneration} currentGeneration={CurrentGeneration}",
                eventName, generation, _generation);
            return;
        }

        LogAudioEvent(eventName, generation, currentTime, duration, readyState, networkState, paused, ended, bufferedEnd, errorCode);
        await ApplyAudioEvent(eventName, generation, currentTime, duration, readyState, networkState, bufferedEnd, errorCode, errorMessage);
    }

    private void CaptureAudioEventState(double? currentTime, double? duration, int readyState, int networkState, int? errorCode, string? errorMessage)
    {
        _lastReadyState = readyState;
        _lastNetworkState = networkState;

        if (currentTime.HasValue)
            _lastCurrentTime = currentTime;

        if (duration.HasValue)
            _lastDuration = duration;

        if (errorCode.HasValue)
            _lastAudioErrorCode = errorCode;

        if (!string.IsNullOrWhiteSpace(errorMessage))
            _lastAudioErrorMessage = errorMessage;
    }

    private void LogAudioEvent(
        string eventName,
        int generation,
        double? currentTime,
        double? duration,
        int readyState,
        int networkState,
        bool paused,
        bool ended,
        double? bufferedEnd,
        int? errorCode)
    {
        var level = eventName == "timeupdate" ? LogLevel.Debug : LogLevel.Information;
        Logger.Log(level,
            "[AudioPlayer] Event event={EventName} gen={Generation} ct={CurrentTime} dur={Duration} rs={ReadyState} ns={NetworkState} paused={Paused} ended={Ended} buf={BufferedEnd} errCode={ErrorCode}",
            eventName, generation, currentTime, duration, readyState, networkState, paused, ended, bufferedEnd, errorCode);
    }

    private async Task ApplyAudioEvent(
        string eventName,
        int generation,
        double? currentTime,
        double? duration,
        int readyState,
        int networkState,
        double? bufferedEnd,
        int? errorCode,
        string? errorMessage)
    {
        var now = DateTime.UtcNow;

        switch (eventName)
        {
            case "timeupdate":
                MarkProgress(now, resetResumeAttempts: false);
                break;
            case "playing":
            case "canplay":
            case "canplaythrough":
                MarkProgress(now, resetResumeAttempts: true);
                break;
            case "waiting":
            case "stalled":
            case "suspend":
                MarkBuffering(now, eventName, generation, currentTime, duration, readyState, networkState, bufferedEnd);
                break;
            case "ended":
                Logger.LogInformation("[AudioPlayer] Ended generation={Generation} ct={CurrentTime} duration={Duration}", generation, currentTime, duration);
                await SetNext("ended");
                break;
            case "error":
                Logger.LogError("[AudioPlayer] Media error generation={Generation} errorCode={ErrorCode} errorMessage={ErrorMessage} ct={CurrentTime} duration={Duration} readyState={ReadyState} networkState={NetworkState}",
                    generation, errorCode, errorMessage, currentTime, duration, readyState, networkState);
                await SetNext($"media-error:{errorCode?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}");
                break;
        }
    }

    private void MarkProgress(DateTime now, bool resetResumeAttempts)
    {
        _lastProgressUtc = now;
        _hasReceivedProgress = true;
        _bufferingStartedUtc = null;
        _isBuffering = false;

        if (resetResumeAttempts)
            _progressTimeoutResumeAttempts = 0;
    }

    private void MarkBuffering(DateTime now, string eventName, int generation, double? currentTime, double? duration, int readyState, int networkState, double? bufferedEnd)
    {
        _bufferingStartedUtc ??= now;
        _isBuffering = true;
        Logger.LogWarning("[AudioPlayer] Buffering started event={EventName} generation={Generation} ct={CurrentTime} duration={Duration} readyState={ReadyState} networkState={NetworkState} bufferedEnd={BufferedEnd}",
            eventName, generation, currentTime, duration, readyState, networkState, bufferedEnd);
    }
}