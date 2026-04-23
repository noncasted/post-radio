using System.Globalization;
using Frontend.Shared;

namespace Frontend.Client.Components;

public partial class AudioPlayer
{
    private async Task<SetNextResult> SetNext(string reason)
    {
        if (_disposed)
            return SetNextResult.Cancelled;

        if (!await _setNextLock.WaitAsync(0))
            return SetNextBusy(reason);

        try
        {
            var snapshot = await TryCaptureSnapshot();
            var previousSong = State.CurrentSong;
            var details = BuildDetails(snapshot, previousSong);

            if (ShouldReportTransitionSkip(reason, previousSong))
                ReportSkip(reason, previousSong, details, snapshot);

            var song = State.PeekNextSong();
            var generation = Interlocked.Increment(ref _generation);
            _audioUrl = string.Empty;
            ResetPlaybackTracking();

            Logger.LogInformation("[AudioPlayer] SetNext reason={Reason} generation={Generation} prevSongId={PrevSongId} nextSongId={NextSongId}",
                reason, generation, previousSong?.Id, song?.Id);

            await InvokeAsync(StateHasChanged);
            await StopAudio();

            if (song == null)
                return await ClearSong();

            return await LoadSong(reason, generation, song);
        }
        catch (OperationCanceledException)
        {
            return SetNextResult.Cancelled;
        }
        finally
        {
            _setNextLock.Release();
        }
    }

    private SetNextResult SetNextBusy(string reason)
    {
        Logger.LogInformation("[AudioPlayer] SetNext skipped reason={Reason} lock=busy generation={Generation} currentSongId={SongId}",
            reason, _generation, State.CurrentSong?.Id);

        _ = PushSkipToBackend(new
        {
            reason,
            title = "SetNext skipped (lock busy)",
            severity = "debug",
            skipped = true,
            lockBusy = true,
            songId = State.CurrentSong?.Id,
            songLabel = AudioPlayerFormatters.FormatSongLabel(State.CurrentSong),
            timestampUtc = DateTime.UtcNow,
            sessionId = State.SessionId,
            generation = _generation,
            recentSkipCount = State.RecentSkips.Count
        });

        return SetNextResult.Busy;
    }

    private async Task<SetNextResult> ClearSong()
    {
        State.SetCurrentSong(null);
        _audioUrl = string.Empty;
        await InvokeAsync(StateHasChanged);
        return SetNextResult.NoSong;
    }

    private async Task<SetNextResult> LoadSong(string reason, int generation, SongDto song)
    {
        var stream = await Api.GetSongStreamUrl(song.Id, ComponentToken);

        if (!stream.IsSuccess)
        {
            Logger.LogWarning("[AudioPlayer] Stream url failed reason={Reason} generation={Generation} songId={SongId} statusCode={StatusCode} notFound={IsNotFound}",
                reason, generation, song.Id, stream.StatusCode, stream.IsNotFound);

            await ReportStreamFailure(reason, song, stream);

            if (stream.IsNotFound)
                State.CommitNextSong(song);

            State.SetCurrentSong(null);
            _audioUrl = string.Empty;
            await InvokeAsync(StateHasChanged);
            return stream.IsNotFound ? SetNextResult.SkippedMissing : SetNextResult.TransientFailure;
        }

        State.CommitNextSong(song);
        State.SetCurrentSong(song);
        _audioUrl = stream.Url;
        await InvokeAsync(StateHasChanged);
        return SetNextResult.Loaded;
    }

    private static bool ShouldReportTransitionSkip(string reason, SongDto? previousSong)
    {
        if (reason == "empty-url")
            return false;

        return previousSong != null;
    }

    private IReadOnlyList<KeyValuePair<string, string?>> BuildDetails(AudioStateSnapshot? snapshot, SongDto? song)
    {
        var context = new AudioSkipDetailContext(
            Generation: _generation,
            Song: song,
            Snapshot: snapshot,
            LastCurrentTime: _lastCurrentTime,
            LastDuration: _lastDuration,
            LastReadyState: _lastReadyState,
            LastNetworkState: _lastNetworkState,
            IsBuffering: _isBuffering,
            BufferingStartedUtc: _bufferingStartedUtc,
            LastProgressUtc: _lastProgressUtc,
            LoadAndPlayCompletedUtc: _loadAndPlayCompletedUtc,
            LastLoadAndPlayError: _lastLoadAndPlayError,
            LastAudioErrorCode: _lastAudioErrorCode,
            LastAudioErrorMessage: _lastAudioErrorMessage,
            IsHidden: _isHidden,
            HiddenSinceUtc: _hiddenSinceUtc,
            ProgressTimeoutResumeAttempts: _progressTimeoutResumeAttempts,
            LastResumeAttemptUtc: _lastResumeAttemptUtc);

        return AudioSkipDetailBuilder.Build(context);
    }
}
