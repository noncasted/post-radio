namespace Meta.Audio;

public class AudioOptions
{
    public string PlaylistsEntryPoint { get; set; } = string.Empty;

    // Optional proxy for SoundCloud traffic (api-v2.soundcloud.com + *.sndcdn.com).
    // Null/empty means direct connection.
    public string? Socks5Proxy { get; set; }
}
