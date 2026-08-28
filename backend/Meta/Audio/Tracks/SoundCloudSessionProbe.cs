using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoundCloudExplode;

namespace Meta.Audio;

public interface ISoundCloudSessionProbe
{
    // Describes the session SoundCloud sees for our requests. Takes a track id
    // because streaming rights are reported per track, and the session claims
    // only travel on a track payload.
    Task<SoundCloudSessionInfo> Describe(long trackId, CancellationToken cancellationToken = default);
}

public sealed record SoundCloudSessionInfo
{
    public required bool AuthorizationConfigured { get; init; }
    public required bool ProxyConfigured { get; init; }
    public required string ClientId { get; init; }
    public string? Geo { get; init; }
    public string? Subject { get; init; }
    public string? Policy { get; init; }
    public string? MonetizationModel { get; init; }
    public string? Error { get; init; }

    // SoundCloud leaves the JWT subject empty for requests without a user
    // session, which is the state that gets served snipped previews.
    public bool IsAnonymous => string.IsNullOrEmpty(Subject);

    public override string ToString()
    {
        var parts = new List<string>
        {
            $"clientId={Mask(ClientId)}",
            $"authorization={(AuthorizationConfigured ? "configured" : "none")}",
            $"proxy={(ProxyConfigured ? "configured" : "direct")}"
        };

        if (Error != null)
        {
            parts.Add($"probeError={Error}");
            return string.Join(", ", parts);
        }

        parts.Add($"session={(IsAnonymous ? "anonymous" : "authenticated")}");
        parts.Add($"geo={Geo ?? "unknown"}");
        parts.Add($"probedTrackPolicy={Policy ?? "unknown"}");
        parts.Add($"probedTrackMonetization={MonetizationModel ?? "unknown"}");

        return string.Join(", ", parts);
    }

    private static string Mask(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return "missing";

        return clientId.Length <= 8
            ? clientId
            : $"{clientId[..4]}...{clientId[^4..]}";
    }
}

public class SoundCloudSessionProbe : ISoundCloudSessionProbe
{
    public SoundCloudSessionProbe(
        HttpClient http,
        SoundCloudClient soundCloud,
        IOptions<AudioOptions> options,
        ILogger<SoundCloudSessionProbe> logger)
    {
        _http = http;
        _soundCloud = soundCloud;
        _options = options.Value;
        _logger = logger;
    }

    private const string TrackEndpoint = "https://api-v2.soundcloud.com/tracks/";

    private readonly HttpClient _http;
    private readonly ILogger<SoundCloudSessionProbe> _logger;
    private readonly AudioOptions _options;
    private readonly SoundCloudClient _soundCloud;

    public async Task<SoundCloudSessionInfo> Describe(long trackId, CancellationToken cancellationToken = default)
    {
        var authorizationConfigured = !string.IsNullOrWhiteSpace(_options.SoundCloudAuthorization);
        var proxyConfigured = !string.IsNullOrWhiteSpace(_options.Socks5Proxy);
        var clientId = _soundCloud.ClientId;

        try
        {
            var uri = $"{TrackEndpoint}{trackId}?client_id={Uri.EscapeDataString(clientId)}";
            using var response = await _http.GetAsync(uri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new SoundCloudSessionInfo
                {
                    AuthorizationConfigured = authorizationConfigured,
                    ProxyConfigured = proxyConfigured,
                    ClientId = clientId,
                    Error = $"probe track request failed with {(int)response.StatusCode} ({response.StatusCode})"
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var (geo, subject) = ReadSessionClaims(root);

            return new SoundCloudSessionInfo
            {
                AuthorizationConfigured = authorizationConfigured,
                ProxyConfigured = proxyConfigured,
                ClientId = clientId,
                Geo = geo,
                Subject = subject,
                Policy = ReadString(root, "policy"),
                MonetizationModel = ReadString(root, "monetization_model")
            };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning(e, "[Audio] [Session] Failed to probe SoundCloud session with track {TrackId}", trackId);

            return new SoundCloudSessionInfo
            {
                AuthorizationConfigured = authorizationConfigured,
                ProxyConfigured = proxyConfigured,
                ClientId = clientId,
                Error = e.Message
            };
        }
    }

    // SoundCloud signs a per-request "track_authorization" JWT whose payload
    // carries the region it resolved us to ("geo") and the user the session is
    // bound to ("sub", empty when anonymous). The signature is theirs to verify,
    // so only the payload is read, purely for diagnostics.
    private static (string? Geo, string? Subject) ReadSessionClaims(JsonElement root)
    {
        var token = ReadString(root, "track_authorization");
        if (string.IsNullOrWhiteSpace(token))
            return (null, null);

        var segments = token.Split('.');
        if (segments.Length < 2)
            return (null, null);

        if (!TryDecodeBase64Url(segments[1], out var payload))
            return (null, null);

        try
        {
            using var document = JsonDocument.Parse(payload);
            return (ReadString(document.RootElement, "geo"), ReadString(document.RootElement, "sub"));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static bool TryDecodeBase64Url(string segment, out string payload)
    {
        payload = string.Empty;

        var normalized = segment.Replace('-', '+').Replace('_', '/');
        var padding = normalized.Length % 4;
        if (padding != 0)
            normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');

        var buffer = new byte[normalized.Length];
        if (!Convert.TryFromBase64String(normalized, buffer, out var written))
            return false;

        payload = Encoding.UTF8.GetString(buffer, 0, written);
        return true;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
