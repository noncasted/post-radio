namespace Meta.Audio;

// Thrown when a SoundCloud download would replace audio that was imported
// outside the loader (console upload, YouTube fallback, local client). The
// local file is the source of truth: Load/redownload must leave it in place.
public class SongLoadingOverriddenException : InvalidOperationException
{
    public SongLoadingOverriddenException(long songId)
        : base($"Song {songId} loading is overridden; refusing to replace imported audio.")
    {
        SongId = songId;
    }

    public long SongId { get; }
}
