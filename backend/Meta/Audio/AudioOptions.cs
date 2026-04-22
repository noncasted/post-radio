namespace Meta.Audio;

public class AudioOptions
{
    public string PlaylistsEntryPoint { get; set; } = string.Empty;

    // Optional SOCKS5 proxy for SoundCloud traffic (api-v2.soundcloud.com + *.sndcdn.com).
    // Set to something like "socks5://127.0.0.1:1080" (dev, local autossh) or
    // "socks5://sc-tunnel:1080" (prod, sidecar container). Null/empty = direct connection.
    public string? Socks5Proxy { get; set; }
}
