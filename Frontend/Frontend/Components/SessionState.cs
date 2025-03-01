using Audio;
using Extensions;

namespace Frontend.Components;

public class SessionState
{
    private readonly ViewableProperty<PlaylistType> _playlist = new(PlaylistType.PostPunk);
    private readonly ViewableProperty<double> _volume = new(75);
    private readonly ViewableProperty<SongMetadata> _currentSong = new(SongMetadata.Empty);

    private readonly ViewableDelegate _started = new();
    private readonly ViewableDelegate _skipRequested = new();
    
    private readonly ILifetime _lifetime = new Lifetime();

    public IViewableProperty<PlaylistType> Playlist => _playlist;
    public IViewableProperty<double> Volume => _volume;
    public IViewableProperty<SongMetadata> CurrentSong => _currentSong;
    public IViewableDelegate SkipRequested => _skipRequested;
    
    public IViewableDelegate Started => _started;
    public ILifetime Lifetime => _lifetime; 
    
    public void SetPlaylist(PlaylistType playlist)
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
    
    public void SetCurrentSong(SongMetadata song)
    {
        _currentSong.Set(song);
    }
}