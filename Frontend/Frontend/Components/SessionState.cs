using Audio;

namespace Frontend.Components;

public class SessionState
{
    public SessionState(ISongsCollection songsCollection)
    {
        _songsCollection = songsCollection;
        _indexOffset = Random.Shared.Next(0, 200);
    }
    
    private readonly ViewableProperty<PlaylistData> _playlist = new();
    private readonly ViewableProperty<double> _volume = new(20);
    private readonly ViewableProperty<SongData> _currentSong = new();

    private readonly ViewableDelegate _started = new();
    private readonly ViewableDelegate _skipRequested = new();
    
    private readonly ILifetime _lifetime = new Lifetime();
    
    private readonly ISongsCollection _songsCollection;

    private readonly int _indexOffset;

    private int _songIndex;
    private int _imageIndex;
    
    public IViewableProperty<PlaylistData> Playlist => _playlist;
    public IViewableProperty<double> Volume => _volume;
    public IViewableProperty<SongData> CurrentSong => _currentSong;
    public IViewableDelegate SkipRequested => _skipRequested;
    
    public IViewableDelegate Started => _started;
    public ILifetime Lifetime => _lifetime; 
    
    public void SetPlaylist(PlaylistData playlist)
    {
        _playlist.Set(playlist);
    }

    public void InvokeStart()
    {
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
        _lifetime.Terminate();
    }

    public SongData IncSongIndex()
    {
        _songIndex += _indexOffset;
        return _songsCollection.ByPlaylist[_playlist.Value.Id][_songIndex % _songsCollection.Count];
    }
    
    public int IncImageIndex()
    {
        _imageIndex += _indexOffset;
        return _imageIndex % _songsCollection.Count;
    }
}