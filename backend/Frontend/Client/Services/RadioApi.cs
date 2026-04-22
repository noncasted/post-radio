using System.Net.Http.Json;
using Frontend.Shared;
using Microsoft.Extensions.Logging;

namespace Frontend.Client.Services;

public interface IRadioApi
{
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

    public async Task<IReadOnlyList<PlaylistDto>> GetPlaylists()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<PlaylistDto>>("/api/radio/playlists");
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
            var result = await _http.GetFromJsonAsync<List<SongDto>>(url);
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
            return await _http.GetStringAsync($"/api/radio/songs/{id}/stream");
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
            var result = await _http.GetFromJsonAsync<ImagesCountDto>("/api/radio/images");
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
            return await _http.GetStringAsync($"/api/radio/images/{index}");
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
            return await _http.GetFromJsonAsync<FrontendOptionsDto>("/api/radio/options");
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "[RadioApi] GetFrontendOptions failed");
            return null;
        }
    }
}
