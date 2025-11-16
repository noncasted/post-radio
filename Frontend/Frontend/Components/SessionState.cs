using Audio;
using Common;
using Images;

namespace Frontend.Components;

public class SessionState
{
    public SessionState(ISongsCollection songsCollection, IImagesCollection imagesCollection)
    {
        _songsCollection = songsCollection;
        _imagesCollection = imagesCollection;
        _indexOffset = Random.Shared.Next(0, 200);
    }

    private readonly ViewableProperty<SongData> _currentSong = new(null);
    private readonly IImagesCollection _imagesCollection;

    private readonly int _indexOffset;

    private readonly ViewableProperty<PlaylistData> _playlist = new(null);
    private readonly ViewableDelegate _skipRequested = new();

    private readonly ISongsCollection _songsCollection;

    private readonly ViewableDelegate _started = new();
    private readonly ViewableProperty<double> _volume = new(20);
    private bool _hasStarted;
    private int _imageIndex;

    private int _songIndex;

    public IViewableProperty<PlaylistData> Playlist => _playlist;
    public IViewableProperty<double> Volume => _volume;
    public IViewableProperty<SongData> CurrentSong => _currentSong;
    public IViewableDelegate SkipRequested => _skipRequested;

    public IViewableDelegate Started => _started;
    public ILifetime Lifetime { get; } = new Lifetime();

    public void SetPlaylist(PlaylistData playlist)
    {
        _playlist.Set(playlist);
    }

    public void InvokeStart()
    {
        if (_hasStarted == true)
            return;

        _hasStarted = true;
        _started.Invoke();
    }

    public void SetVolume(double volume)
    {
        _volume.Set(volume);
    }

    public void RequestSkip()
    {
        _skipRequested.Invoke();
    }

    public void SetCurrentSong(SongData song)
    {
        _currentSong.Set(song);
    }

    public void Dispose()
    {
        Lifetime.Terminate();
    }

    public SongData IncSongIndex()
    {
        _songIndex += _indexOffset + 1;
        var playlist = _songsCollection.ByPlaylist[_playlist.Value.Id];
        var index = _songIndex % playlist.Count;
        var data = playlist[index];
        return data;
    }

    public int IncImageIndex()
    {
        _imageIndex += _indexOffset;
        return _imageIndex % _imagesCollection.Count;
    }
}