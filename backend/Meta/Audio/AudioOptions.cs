namespace Meta.Audio;

public class AudioOptions
{
    public string PlaylistsEntryPoint { get; set; } = string.Empty;

    // Optional absolute or relative path to the song metadata lookup file.
    // Defaults to tools/metadata/songs.json beside the published service, with
    // a repository-root fallback for local development runs from bin/Debug.
    public string? SongLookupFile { get; set; }

    // Optional proxy for SoundCloud traffic (api-v2.soundcloud.com + *.sndcdn.com).
    // Null/empty means direct connection.
    public string? Socks5Proxy { get; set; }
}
