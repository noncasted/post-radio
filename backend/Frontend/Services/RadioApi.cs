using System.Net;
using System.Net.Http.Json;
using Frontend.Shared;
using Microsoft.Extensions.Logging;

namespace Frontend.Services;

public sealed record SongStreamUrlResult(bool IsSuccess, string Url, bool IsNotFound, int? StatusCode)
{
    public static SongStreamUrlResult Success(string url, int statusCode) => new(true, url, false, statusCode);

    public static SongStreamUrlResult Failure(HttpStatusCode? statusCode = null) => new(false, string.Empty, false,
        statusCode.HasValue ? (int)statusCode.Value : null);

    public static SongStreamUrlResult NotFound() => new(false, string.Empty, true, (int)HttpStatusCode.NotFound);
}

public interface IRadioApi
{
    Task<IReadOnlyList<PlaylistDto>> GetPlaylists();
    Task<IReadOnlyList<SongDto>> GetSongs(Guid? playlistId = null);
    Task<SongStreamUrlResult> GetSongStreamUrl(long id, CancellationToken cancellationToken = default);
    Task<ImagesBatchDto> GetImageBatch(int start, int count);
    Task<FrontendOptionsDto?> GetFrontendOptions();
    Task ReportSkip(object payload, CancellationToken cancellationToken = default);
}

public class RadioApi : IRadioApi
{
    public RadioApi(HttpClient http, ILogger<RadioApi> logger)
    {
        _http = http;
        _logger = logger;
    }

    private static readonly ImagesBatchDto EmptyImageBatch = new() { Urls = Array.Empty<string>() };

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
            return (result ?? new List<SongDto>())
                   .Where(PlayableTrackPolicy.IsPlayable)
                   .ToList();
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
            using var response = await _http.GetAsync($"/api/radio/songs/{id}/stream",
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return SongStreamUrlResult.NotFound();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[RadioApi] GetSongStreamUrl failed for {SongId}: {StatusCode}", id,
                    response.StatusCode);
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

    public async Task<ImagesBatchDto> GetImageBatch(int start, int count)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<ImagesBatchDto>(
                $"/api/radio/images/batch?start={start}&count={count}");

            return result ?? EmptyImageBatch;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "[RadioApi] GetImageBatch failed");
            return EmptyImageBatch;
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

    public async Task ReportSkip(object payload, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync("/api/radio/skip-report",
                payload,
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "[RadioApi] ReportSkip failed");
        }
    }

}