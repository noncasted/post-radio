using System.Net.Http.Json;
using Frontend.Shared;
using Microsoft.Extensions.Logging;

namespace Frontend.Client.Services;

public interface IRadioApi
{
    void SetSessionId(string sessionId);
    Task TouchPresence();
    Task<IReadOnlyList<PlaylistDto>> GetPlaylists();
    Task<IReadOnlyList<SongDto>> GetSongs(Guid? playlistId = null);
    Task<string> GetSongStreamUrl(long id);
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

    public async Task<string> GetSongStreamUrl(long id)
    {
        try
        {
            return await _http.GetStringAsync(WithSession($"/api/radio/songs/{id}/stream"));
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "[RadioApi] GetSongStreamUrl failed");
            return string.Empty;
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
