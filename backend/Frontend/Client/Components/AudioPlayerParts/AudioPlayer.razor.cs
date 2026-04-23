using Frontend.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Frontend.Client.Components;

public partial class AudioPlayer : IAsyncDisposable
{
    [Parameter] public required SessionState State { get; set; }

    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private IRadioApi Api { get; set; } = null!;
    [Inject] private ILogger<AudioPlayer> Logger { get; set; } = null!;

    private readonly SemaphoreSlim _setNextLock = new(1, 1);

    private string _audioUrl = string.Empty;
    private int _generation;
    private DateTime _componentStartUtc = DateTime.UtcNow;
    private DateTime? _loadAndPlayCompletedUtc;
    private bool _isHidden;
    private DateTime? _hiddenSinceUtc;
    private int _progressTimeoutResumeAttempts;
    private DateTime? _lastResumeAttemptUtc;
    private DateTime _lastProgressUtc = DateTime.MinValue;
    private DateTime? _bufferingStartedUtc;
    private bool _isBuffering;
    private bool _hasReceivedProgress;
    private int? _lastReadyState;
    private int? _lastNetworkState;
    private double? _lastCurrentTime;
    private double? _lastDuration;
    private int? _lastAudioErrorCode;
    private string? _lastAudioErrorMessage;
    private string? _lastLoadAndPlayError;
    private bool _disposed;
    private CancellationTokenSource? _componentCts;
    private DotNetObjectReference<AudioPlayer>? _dotNetRef;
    private Task? _loopTask;

    private Action? _startedHandler;
    private Action? _skipRequestedHandler;
    private Action? _playlistChangedHandler;
    private Action? _volumeChangedHandler;

    private CancellationToken ComponentToken => _componentCts?.Token ?? State.Token;

    protected override void OnInitialized()
    {
        _componentStartUtc = DateTime.UtcNow;
        _componentCts = CancellationTokenSource.CreateLinkedTokenSource(State.Token);

        _startedHandler = () => {
            Logger.LogInformation("[AudioPlayer] Trigger source=Started loopRunning={LoopRunning}", _loopTask is { IsCompleted: false });
            EnsureLoopStarted();
        };
        _skipRequestedHandler = () => {
            Logger.LogInformation("[AudioPlayer] Trigger source=SkipRequested generation={Generation} currentSongId={SongId}", _generation, State.CurrentSong?.Id);
            _ = SetNext("skip-requested");
        };
        _playlistChangedHandler = () => {
            Logger.LogInformation("[AudioPlayer] Trigger source=PlaylistChanged generation={Generation} playlist={Playlist}", _generation, State.Playlist?.Name);
            _ = SetNext("playlist-changed");
        };
        _volumeChangedHandler = () => _ = OnVolumeChanged(State.Volume);

        State.Started += _startedHandler;
        State.SkipRequested += _skipRequestedHandler;
        State.PlaylistChanged += _playlistChangedHandler;
        State.VolumeChanged += _volumeChangedHandler;
    }

    private void EnsureLoopStarted()
    {
        if (_loopTask is { IsCompleted: false })
            return;

        _loopTask = Loop();
    }

    private async Task OnVolumeChanged(double value)
    {
        if (_disposed)
            return;

        var ratio = Math.Clamp(value / 100, 0, 1);
        var options = State.Options;
        var volume = ratio * options.MaxVolume;

        try
        {
            await Js.InvokeVoidAsync("audioHelper.setVolume", Math.Clamp(volume, 0, 1));
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        UnsubscribeStateEvents();

        try
        {
            _componentCts?.Cancel();
            await WaitForLoopToStop();
            await Js.InvokeVoidAsync("audioHelper.detach");
        }
        catch (JSDisconnectedException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        _dotNetRef?.Dispose();
        _componentCts?.Dispose();
        _setNextLock.Dispose();
    }

    private void UnsubscribeStateEvents()
    {
        if (_startedHandler != null)
            State.Started -= _startedHandler;
        if (_skipRequestedHandler != null)
            State.SkipRequested -= _skipRequestedHandler;
        if (_playlistChangedHandler != null)
            State.PlaylistChanged -= _playlistChangedHandler;
        if (_volumeChangedHandler != null)
            State.VolumeChanged -= _volumeChangedHandler;
    }

    private async Task WaitForLoopToStop()
    {
        if (_loopTask == null)
            return;

        try
        {
            await _loopTask;
        }
        catch (OperationCanceledException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }
}