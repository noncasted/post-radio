using System.Net;
using System.Net.Http.Json;
using Frontend.Shared;
using Microsoft.Extensions.Logging;

namespace Frontend.Client.Services;

public sealed record SongStreamUrlResult(bool IsSuccess, string Url, bool IsNotFound, int? StatusCode)
{
    public static SongStreamUrlResult Success(string url, int statusCode) => new(true, url, false, statusCode);
    public static SongStreamUrlResult Failure(HttpStatusCode? statusCode = null) => new(false, string.Empty, false, statusCode.HasValue ? (int)statusCode.Value : null);
    public static SongStreamUrlResult NotFound() => new(false, string.Empty, true, (int)HttpStatusCode.NotFound);
}

public interface IRadioApi
{
    void SetSessionId(string sessionId);
    Task TouchPresence();
    Task<IReadOnlyList<PlaylistDto>> GetPlaylists();
    Task<IReadOnlyList<SongDto>> GetSongs(Guid? playlistId = null);
    Task<SongStreamUrlResult> GetSongStreamUrl(long id, CancellationToken cancellationToken = default);
    Task<int> GetImagesCount();
    Task<string> GetImageUrl(int index);
    Task<FrontendOptionsDto?> GetFrontendOptions();
}

public class RadioApi : IRadioApi
{
    public RadioApi(HttpClient http, ILogger<RadioApi> logger)
    {
        _http = http;
        _logger = logger;
    }

    private readonly HttpClient _http;
    private readonly ILogger<RadioApi> _logger;
    private string? _sessionId;

    public void SetSessionId(string sessionId)
    {
        _sessionId = sessionId;
    }

    public async Task TouchPresence()
    {
        try
        {
            using var response = await _http.PostAsync(WithSession("/api/radio/presence/touch"), null);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "[RadioApi] TouchPresence failed");
        }
    }

    public async Task<IReadOnlyList<PlaylistDto>> GetPlaylists()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<PlaylistDto>>(WithSession("/api/radio/playlists"));
            return result ?? new List<PlaylistDto>();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "[RadioApi] GetPlaylists failed");
            return new List<PlaylistDto>();
        }
    }

    public async Task<IReadOnlyList<SongDto>> GetSongs(Guid? playlistId = null)
    {
        var url = playlistId.HasValue ? $"/api/radio/songs?playlistId={playlistId}" : "/api/radio/songs";
        try
        {
            var result = await _http.GetFromJsonAsync<List<SongDto>>(WithSession(url));
            return result ?? new List<SongDto>();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "[RadioApi] GetSongs failed");
            return new List<SongDto>();
        }
    }

    public async Task<SongStreamUrlResult> GetSongStreamUrl(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync(WithSession($"/api/radio/songs/{id}/stream"), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return SongStreamUrlResult.NotFound();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[RadioApi] GetSongStreamUrl failed for {SongId}: {StatusCode}", id, response.StatusCode);
                return SongStreamUrlResult.Failure(response.StatusCode);
            }

            var url = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(url))
            {
                _logger.LogWarning("[RadioApi] GetSongStreamUrl returned an empty url for {SongId}", id);
                return SongStreamUrlResult.Failure(response.StatusCode);
            }

            return SongStreamUrlResult.Success(url, (int)response.StatusCode);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "[RadioApi] GetSongStreamUrl failed");
            return SongStreamUrlResult.Failure();
        }
    }

    public async Task<int> GetImagesCount()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<ImagesCountDto>(WithSession("/api/radio/images"));
            return result?.Count ?? 0;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "[RadioApi] GetImagesCount failed");
            return 0;
        }
    }

    public async Task<string> GetImageUrl(int index)
    {
        try
        {
            return await _http.GetStringAsync(WithSession($"/api/radio/images/{index}"));
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "[RadioApi] GetImageUrl failed");
            return string.Empty;
        }
    }

    public async Task<FrontendOptionsDto?> GetFrontendOptions()
    {
        try
        {
            return await _http.GetFromJsonAsync<FrontendOptionsDto>(WithSession("/api/radio/options"));
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "[RadioApi] GetFrontendOptions failed");
            return null;
        }
    }

    private string WithSession(string url)
    {
        if (string.IsNullOrWhiteSpace(_sessionId))
            return url;

        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}sid={Uri.EscapeDataString(_sessionId)}";
    }
}
