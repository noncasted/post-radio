namespace Meta.Audio;

public class AudioOptions
{
    public string PlaylistsEntryPoint { get; set; } = string.Empty;

    // Optional absolute or relative path to the song metadata lookup file.
    // Defaults to tools/metadata/songs.json beside the published service, with
    // a repository-root fallback for local development runs from bin/Debug.
    public string? SongLookupFile { get; set; }

    // Optional SoundCloud client id. If empty, SoundCloudExplode resolves the
    // current public client id during initialization.
    public string? SoundCloudClientId { get; set; }

    // Optional full Authorization header value for SoundCloud requests, for
    // example "OAuth <token>" or "Bearer <token>". Never store real values in
    // repository appsettings; pass with environment config/secrets.
    public string? SoundCloudAuthorization { get; set; }

    // Optional proxy for SoundCloud traffic (api-v2.soundcloud.com + *.sndcdn.com).
    // Null/empty means direct connection.
    public string? Socks5Proxy { get; set; }
}
