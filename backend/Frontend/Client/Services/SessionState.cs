using Frontend.Shared;

namespace Frontend.Client.Services;

public class SessionState : IDisposable
{
    public SessionState(IRadioApi api)
    {
        _api = api;
        SessionId = Guid.NewGuid().ToString("N");
        _api.SetSessionId(SessionId);
    }

    private readonly IRadioApi _api;
    private readonly CancellationTokenSource _cts = new();
    private readonly Random _random = new();

    private List<SongDto> _playlistSongs = new();
    private int _songIndex;
    private int _imageIndex = Random.Shared.Next();

    public CancellationToken Token => _cts.Token;
    public string SessionId { get; }

    public PlaylistDto? Playlist { get; private set; }
    public SongDto? CurrentSong { get; private set; }
    public double Volume { get; private set; } = 50;

    public FrontendOptionsDto Options { get; private set; } = new()
    {
        BaseVolume = 0.5f,
        MaxVolume = 1.0f,
        ImageSwitchIntervalMs = 8000,
        ImageFadeMs = 1000
    };

    public event Action? Started;
    public event Action? SkipRequested;
    public event Action? PlaylistChanged;
    public event Action? VolumeChanged;
    public event Action? CurrentSongChanged;
    public event Action? OptionsChanged;

    private bool _isStarted;

    public async Task LoadOptions()
    {
        var options = await _api.GetFrontendOptions();

        if (options == null)
            return;

        Options = options;
        Volume = Math.Clamp(options.BaseVolume, 0, 1) * 100;
        OptionsChanged?.Invoke();
        VolumeChanged?.Invoke();
    }

    public void InvokeStart()
    {
        if (_isStarted)
            return;

        _isStarted = true;
        TouchLoop();
        Started?.Invoke();
    }

    public void RequestSkip() => SkipRequested?.Invoke();

    public async Task SetPlaylist(PlaylistDto playlist)
    {
        Playlist = playlist;
        _playlistSongs = (await _api.GetSongs(playlist.Id)).ToList();
        Shuffle(_playlistSongs);
        _songIndex = 0;
        PlaylistChanged?.Invoke();
    }

    public void SetVolume(double value)
    {
        Volume = value;
        VolumeChanged?.Invoke();
    }

    public SongDto? PeekNextSong()
    {
        if (_playlistSongs.Count == 0)
            return null;

        return _playlistSongs[_songIndex % _playlistSongs.Count];
    }

    public void CommitNextSong(SongDto? song)
    {
        if (song == null || _playlistSongs.Count == 0)
            return;

        var currentIndex = _songIndex % _playlistSongs.Count;
        if (_playlistSongs[currentIndex].Id == song.Id)
        {
            _songIndex++;
            return;
        }

        var songIndex = _playlistSongs.FindIndex(candidate => candidate.Id == song.Id);
        if (songIndex >= 0)
            _songIndex = songIndex + 1;
    }

    public int IncImageIndex(int total)
    {
        if (total == 0)
            return 0;

        _imageIndex = (_imageIndex + 1) % total;
        return _imageIndex;
    }

    public void SetCurrentSong(SongDto? song)
    {
        CurrentSong = song;
        CurrentSongChanged?.Invoke();
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void TouchLoop()
    {
        _ = RunTouchLoop();
    }

    private async Task RunTouchLoop()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                await _api.TouchPresence();
                await Task.Delay(TimeSpan.FromSeconds(60), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(60), _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
