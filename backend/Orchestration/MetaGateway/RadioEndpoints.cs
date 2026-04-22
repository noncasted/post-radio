using Cluster.Configs;
using Common;
using Meta.Audio;
using Meta.Images;
using Meta.Online;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace MetaGateway;

public static class RadioEndpoints
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();
    public static IEndpointRouteBuilder AddRadioEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/radio");

        group.MapGet("/playlists", ListPlaylists);
        group.MapGet("/songs", ListSongs);
        group.MapGet("/songs/{id:long}/stream", GetSongStream);
        group.MapGet("/images", ListImages);
        group.MapGet("/images/{index:int}", GetImageUrl);
        group.MapGet("/media/audio/{id:long}", GetAudioFile);
        group.MapGet("/media/images/{key}", GetImageFile);
        group.MapGet("/options", GetOptions);
        group.MapPost("/presence/touch", TouchPresence);

        return builder;
    }

    private static FrontendOptionsDto GetOptions(
        [FromServices] IFrontendConfig config,
        [FromServices] IOnlineTracker onlineTracker,
        HttpContext context)
    {
        Touch(onlineTracker, context);
        var value = config.Value;

        return new FrontendOptionsDto
        {
            BaseVolume = value.BaseVolume,
            MaxVolume = value.MaxVolume,
            ImageSwitchIntervalMs = value.ImageSwitchIntervalMs,
            ImageFadeMs = value.ImageFadeMs
        };
    }

    private static IReadOnlyList<PlaylistDto> ListPlaylists(
        [FromServices] IPlaylistsCollection collection,
        [FromServices] IOnlineTracker onlineTracker,
        HttpContext context)
    {
        Touch(onlineTracker, context);
        return collection
               .Select(kv => new PlaylistDto { Id = kv.Key, Name = kv.Value.Name, Url = kv.Value.Url })
               .ToList();
    }

    private static IReadOnlyList<SongDto> ListSongs(
        [FromServices] ISongsCollection collection,
        [FromServices] IMediaStorage storage,
        [FromServices] IOnlineTracker onlineTracker,
        HttpContext context,
        [FromQuery] Guid? playlistId)
    {
        Touch(onlineTracker, context);
        var source = playlistId.HasValue
            ? collection.Where(kv => kv.Value.Playlists.Contains(playlistId.Value))
            : collection;

        return source
               .Where(kv => kv.Value.IsLoaded && File.Exists(storage.GetAudioPath(kv.Key)))
               .Select(kv => new SongDto
               {
                   Id = kv.Key,
                   Author = kv.Value.Author,
                   Name = kv.Value.Name,
                   Url = kv.Value.Url,
                   Playlists = kv.Value.Playlists,
                   AddDate = kv.Value.AddDate
               })
               .ToList();
    }

    private static IResult GetSongStream(
        [FromServices] IMediaStorage storage,
        [FromServices] IOnlineTracker onlineTracker,
        HttpContext context,
        long id)
    {
        if (!File.Exists(storage.GetAudioPath(id)))
            return Results.NotFound();

        var sessionId = Touch(onlineTracker, context);
        return Results.Text(AppendSessionId(storage.GetAudioUrl(id), sessionId), "text/plain");
    }

    private static IResult GetAudioFile(
        [FromServices] IMediaStorage storage,
        [FromServices] IOnlineTracker onlineTracker,
        HttpContext context,
        long id)
    {
        var path = storage.GetAudioPath(id);

        if (!File.Exists(path))
            return Results.NotFound();

        Touch(onlineTracker, context);
        return Results.File(path, "audio/mpeg", enableRangeProcessing: true);
    }

    private static IResult GetImageFile(
        [FromServices] IMediaStorage storage,
        [FromServices] IOnlineTracker onlineTracker,
        HttpContext context,
        string key)
    {
        var path = storage.GetImagePath(key);

        if (!File.Exists(path))
            return Results.NotFound();

        Touch(onlineTracker, context);

        if (!ContentTypes.TryGetContentType(path, out var contentType))
            contentType = "application/octet-stream";

        return Results.File(path, contentType, enableRangeProcessing: true);
    }

    private static ImagesCountDto ListImages(
        [FromServices] IImagesCollection collection,
        [FromServices] IOnlineTracker onlineTracker,
        HttpContext context)
    {
        Touch(onlineTracker, context);
        return new ImagesCountDto { Count = collection.Count };
    }

    private static async Task<string> GetImageUrl(
        [FromServices] IImagesCollection collection,
        [FromServices] IOnlineTracker onlineTracker,
        HttpContext context,
        int index)
    {
        var sessionId = Touch(onlineTracker, context);
        return AppendSessionId(await collection.GetUrl(index), sessionId);
    }

    private static IResult TouchPresence(
        [FromServices] IOnlineTracker onlineTracker,
        HttpContext context)
    {
        Touch(onlineTracker, context);
        return Results.NoContent();
    }

    private static string? Touch(IOnlineTracker onlineTracker, HttpContext context)
    {
        var sessionId = GetSessionId(context);
        onlineTracker.Touch(sessionId);
        return sessionId;
    }

    private static string? GetSessionId(HttpContext context)
    {
        if (context.Request.Query.TryGetValue("sid", out var querySessionId))
            return querySessionId.ToString();

        if (context.Request.Headers.TryGetValue("X-Radio-Session-Id", out var headerSessionId))
            return headerSessionId.ToString();

        return context.Request.Cookies.TryGetValue("Radio.SessionId", out var cookieSessionId)
            ? cookieSessionId
            : null;
    }

    private static string AppendSessionId(string url, string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return url;

        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}sid={Uri.EscapeDataString(sessionId)}";
    }
}

public class PlaylistDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
}

public class SongDto
{
    public required long Id { get; init; }
    public required string Author { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public required IReadOnlyList<Guid> Playlists { get; init; }
    public required DateTime AddDate { get; init; }
}

public class ImagesCountDto
{
    public required int Count { get; init; }
}

public class FrontendOptionsDto
{
    public required float BaseVolume { get; init; }
    public required float MaxVolume { get; init; }
    public required int ImageSwitchIntervalMs { get; init; }
    public required int ImageFadeMs { get; init; }
}
