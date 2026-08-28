using Frontend.Shared;

namespace Frontend.Components;

public partial class AudioPlayer
{
    /// <summary>
    /// Tops the JS queue back up to <see cref="AudioPlayerConfig.QueueTarget"/>. Runs on the
    /// .NET side only: picking a track and resolving its stream url is the whole remaining job.
    /// </summary>
    private async Task EnsureQueue(string reason)
    {
        if (_disposed || !_initialized)
            return;

        if (!await _queueLock.WaitAsync(0))
            return;

        try
        {
            var appended = await BuildQueueItems(reason);

            if (appended.Count == 0)
                return;

            _queuedCount += appended.Count;
            await InvokeJs("radioPlayer.enqueue", appended);

            Logger.LogInformation("[AudioPlayer] Enqueued reason={Reason} count={Count} queued={Queued}",
                reason, appended.Count, _queuedCount);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Logger.LogError(e, "[AudioPlayer] EnsureQueue failed reason={Reason}", reason);
        }
        finally
        {
            _queueLock.Release();
        }
    }

    private async Task<List<QueuedTrack>> BuildQueueItems(string reason)
    {
        var appended = new List<QueuedTrack>();
        var lookups = 0;

        while (_queuedCount + appended.Count < AudioPlayerConfig.QueueTarget
               && lookups < AudioPlayerConfig.MaxLookupsPerRefill
               && !ComponentToken.IsCancellationRequested)
        {
            lookups++;
            var song = State.PeekNextSong();

            if (song == null)
            {
                OnPlaylistEmpty(reason);
                break;
            }

            var stream = await Api.GetSongStreamUrl(song.Id, ComponentToken);

            if (!stream.IsSuccess)
            {
                ReportStreamFailure(reason, song, stream);

                if (stream.IsNotFound)
                {
                    // Known-bad track: step over it and try the next one right away.
                    State.CommitNextSong(song);
                    continue;
                }

                break;
            }

            State.CommitNextSong(song);
            var token = Interlocked.Increment(ref _token);

            lock (_tracksLock)
                _tracks[token] = song;

            appended.Add(new QueuedTrack(token, song.Id, stream.Url));
        }

        if (appended.Count > 0)
            _refillRetryCount = 0;

        return appended;
    }

    private void OnPlaylistEmpty(string reason)
    {
        _refillRetryCount++;
        var delay = AudioPlayerConfig.GetRefillRetryDelay(_refillRetryCount);

        Logger.LogInformation("[AudioPlayer] Playlist empty reason={Reason} retry={Retry} delay={Delay}",
            reason, _refillRetryCount, delay);

        _ = RetryQueueLater(delay);
    }

    private async Task RetryQueueLater(TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, ComponentToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await EnsureQueue("retry");
    }

    private SongDto? ForgetTrack(long token)
    {
        SongDto? song;

        lock (_tracksLock)
            _tracks.Remove(token, out song);

        // Tokens issued before a playlist switch no longer count against the queue.
        if (token > _barrierToken)
            _queuedCount = Math.Max(0, _queuedCount - 1);

        return song;
    }
}
