using Frontend.Services;
using Frontend.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Frontend.Components;

/// <summary>
/// Track chooser only. Playback, the watchdog and the resume probe live in
/// <c>wwwroot/js/radio-player.js</c>; this component talks to it twice per track.
/// </summary>
public partial class AudioPlayer : IAsyncDisposable
{
    [Parameter] public required SessionState State { get; set; }

    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private IRadioApi Api { get; set; } = null!;
    [Inject] private ILogger<AudioPlayer> Logger { get; set; } = null!;

    private readonly SemaphoreSlim _queueLock = new(1, 1);
    private readonly Dictionary<long, SongDto> _tracks = new();
    private readonly object _tracksLock = new();

    private CancellationTokenSource? _componentCts;
    private DotNetObjectReference<AudioPlayer>? _dotNetRef;

    private long _token;
    private long _barrierToken;
    private int _queuedCount;
    private int _refillRetryCount;
    private bool _initialized;
    private bool _disposed;

    private Action? _startedHandler;
    private Action? _skipRequestedHandler;
    private Action? _playlistChangedHandler;
    private Action? _volumeChangedHandler;

    private CancellationToken ComponentToken => _componentCts?.Token ?? State.Token;

    protected override void OnInitialized()
    {
        _componentCts = CancellationTokenSource.CreateLinkedTokenSource(State.Token);

        _startedHandler = () => _ = OnStarted();
        _skipRequestedHandler = () => _ = OnSkipRequested();
        _playlistChangedHandler = () => _ = OnPlaylistChanged();
        _volumeChangedHandler = () => _ = OnVolumeChanged(State.Volume);

        State.Started += _startedHandler;
        State.SkipRequested += _skipRequestedHandler;
        State.PlaylistChanged += _playlistChangedHandler;
        State.VolumeChanged += _volumeChangedHandler;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed)
            return;

        _dotNetRef = DotNetObjectReference.Create(this);
        var config = new AudioPlayerJsConfig { Volume = GetVolumeRatio(State.Volume) };

        try
        {
            await Js.InvokeVoidAsync("radioPlayer.init", _dotNetRef, config);
        }
        catch (JSDisconnectedException)
        {
            return;
        }

        _initialized = true;
        Logger.LogInformation("[AudioPlayer] Initialized session={SessionId} queueTarget={QueueTarget}",
            State.SessionId, AudioPlayerConfig.QueueTarget);

        await EnsureQueue("initialized");
    }

    private async Task OnStarted()
    {
        Logger.LogInformation("[AudioPlayer] Start requested queued={Queued}", _queuedCount);

        await EnsureQueue("started");
        await InvokeJs("radioPlayer.start");
    }

    private async Task OnSkipRequested()
    {
        Logger.LogInformation("[AudioPlayer] Skip requested songId={SongId}", State.CurrentSong?.Id);
        await InvokeJs("radioPlayer.skip");
    }

    private async Task OnPlaylistChanged()
    {
        Logger.LogInformation("[AudioPlayer] Playlist changed playlist={Playlist}", State.Playlist?.Name);

        // Everything already handed to JS belongs to the previous playlist.
        _barrierToken = Interlocked.Read(ref _token);
        _queuedCount = 0;
        _refillRetryCount = 0;

        lock (_tracksLock)
            _tracks.Clear();

        State.SetCurrentSong(null);
        await InvokeJs("radioPlayer.reset", "playlist-changed");
        await EnsureQueue("playlist-changed");
    }

    private async Task OnVolumeChanged(double value)
    {
        await InvokeJs("radioPlayer.setVolume", GetVolumeRatio(value));
    }

    private double GetVolumeRatio(double value)
    {
        var ratio = Math.Clamp(value / 100, 0, 1);
        return Math.Clamp(ratio * State.Options.MaxVolume, 0, 1);
    }

    private async Task InvokeJs(string identifier, params object?[] args)
    {
        if (_disposed || !_initialized)
            return;

        try
        {
            await Js.InvokeVoidAsync(identifier, args);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "[AudioPlayer] JS call failed identifier={Identifier}", identifier);
        }
    }

    private SongDto? TryGetTrack(long token)
    {
        lock (_tracksLock)
            return _tracks.TryGetValue(token, out var song) ? song : null;
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
            await Js.InvokeVoidAsync("radioPlayer.dispose");
        }
        catch (JSDisconnectedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (TaskCanceledException)
        {
        }

        _dotNetRef?.Dispose();
        _componentCts?.Dispose();
        _queueLock.Dispose();
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
}
