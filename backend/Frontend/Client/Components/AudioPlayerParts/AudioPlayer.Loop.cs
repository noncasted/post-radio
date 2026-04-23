using Microsoft.JSInterop;

namespace Frontend.Client.Components;

public partial class AudioPlayer
{
    private async Task Loop()
    {
        _dotNetRef ??= DotNetObjectReference.Create(this);

        await Js.InvokeVoidAsync("audioHelper.setAudioElement", _dotNetRef);
        await OnVolumeChanged(State.Volume);

        var emptyUrlRetryCount = 0;
        var skippedMissingCount = 0;

        while (!ComponentToken.IsCancellationRequested)
        {
            try
            {
                var setNextResult = SetNextResult.None;
                if (string.IsNullOrWhiteSpace(_audioUrl))
                    setNextResult = await SetNext("empty-url");

                if (string.IsNullOrWhiteSpace(_audioUrl))
                {
                    if (setNextResult == SetNextResult.SkippedMissing)
                    {
                        emptyUrlRetryCount = 0;
                        skippedMissingCount++;

                        if (skippedMissingCount >= 3)
                            await Task.Delay(AudioPlayerTiming.EmptyPlaylistDelay, ComponentToken);

                        continue;
                    }

                    skippedMissingCount = 0;
                    emptyUrlRetryCount++;
                    var delay = AudioPlayerTiming.GetEmptyUrlDelay(emptyUrlRetryCount);
                    var isStartupWindow = AudioPlayerTiming.IsStartupWindow(_componentStartUtc);
                    Logger.LogInformation(
                        "[AudioPlayer] Empty url retry count={Count} delay={Delay} startupWindow={StartupWindow}",
                        emptyUrlRetryCount, delay, isStartupWindow);
                    await Task.Delay(delay, ComponentToken);
                    continue;
                }

                emptyUrlRetryCount = 0;
                skippedMissingCount = 0;
                var generation = _generation;
                var url = _audioUrl;
                ResetPlaybackTracking();
                Logger.LogInformation("[AudioPlayer] LoadAndPlay begin generation={Generation} url={Url}", generation, url);

                var result = await Js.InvokeAsync<LoadAndPlayResult>("audioHelper.loadAndPlay", url, generation);
                CaptureLoadAndPlayResult(result);

                if (!result.Started)
                {
                    await HandleLoadAndPlayFailure(generation, url, result);
                    continue;
                }

                _loadAndPlayCompletedUtc = DateTime.UtcNow;
                Logger.LogInformation("[AudioPlayer] LoadAndPlay started generation={Generation} readyState={ReadyState} networkState={NetworkState}",
                    generation, result.ReadyState, result.NetworkState);

                await WatchCurrentTrack(generation, url);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (JSDisconnectedException)
            {
                return;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "[AudioPlayer] Loop failed");
                await RecoverAfterLoopFailure();
            }
        }
    }

    private void CaptureLoadAndPlayResult(LoadAndPlayResult result)
    {
        _lastReadyState = result.ReadyState;
        _lastNetworkState = result.NetworkState;

        if (result.AudioErrorCode.HasValue)
            _lastAudioErrorCode = result.AudioErrorCode;

        if (!string.IsNullOrWhiteSpace(result.AudioErrorMessage))
            _lastAudioErrorMessage = result.AudioErrorMessage;
    }

    private async Task HandleLoadAndPlayFailure(int generation, string url, LoadAndPlayResult result)
    {
        _lastLoadAndPlayError = result.ErrorName;
        Logger.LogWarning("[AudioPlayer] LoadAndPlay failed generation={Generation} errorName={ErrorName} errorMessage={ErrorMessage} readyState={ReadyState} networkState={NetworkState} audioErrCode={AudioErrCode}",
            generation, result.ErrorName, result.ErrorMessage, result.ReadyState, result.NetworkState, result.AudioErrorCode);

        if (IsCurrent(generation, url))
            await SetNext($"play-failed:{result.ErrorName ?? "unknown"}");

        await Task.Delay(AudioPlayerTiming.PlayFailureDelay, ComponentToken);
    }

    private async Task WatchCurrentTrack(int generation, string url)
    {
        while (IsCurrent(generation, url) && !ComponentToken.IsCancellationRequested)
        {
            await Task.Delay(AudioPlayerTiming.WatchdogInterval, ComponentToken);

            if (!TryGetWatchdogReason(out var reason))
                continue;

            Logger.LogWarning("[AudioPlayer] Watchdog trip reason={Reason} generation={Generation} url={Url} hidden={Hidden} hasProgress={HasProgress} isBuffering={IsBuffering} lastProgressAgoSec={LastProgressAgoSec} bufferingAgoSec={BufferingAgoSec} startedAgoSec={StartedAgoSec} readyState={ReadyState} networkState={NetworkState}",
                reason, generation, url, _isHidden, _hasReceivedProgress, _isBuffering,
                AudioPlayerTiming.GetSecondsSince(_lastProgressUtc), AudioPlayerTiming.GetSecondsSince(_bufferingStartedUtc),
                AudioPlayerTiming.GetSecondsSince(_loadAndPlayCompletedUtc), _lastReadyState, _lastNetworkState);

            if (_isHidden)
            {
                Logger.LogInformation("[AudioPlayer] Watchdog suppressed reason={Reason} hidden=true generation={Generation}", reason, generation);
                _lastProgressUtc = DateTime.UtcNow;
                _bufferingStartedUtc = _isBuffering ? DateTime.UtcNow : null;
                continue;
            }

            if (reason == AudioPlayerTiming.ProgressTimeoutReason && await TryResumeBeforeSkip(generation, url))
                continue;

            await SetNext(reason);
            break;
        }
    }
}