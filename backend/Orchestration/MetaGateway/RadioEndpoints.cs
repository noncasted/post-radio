using Cluster.Configs;
using Common;
using Meta.Audio;
using Meta.Images;
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

        return builder;
    }

    private static FrontendOptionsDto GetOptions([FromServices] IFrontendConfig config)
    {
        var value = config.Value;

        return new FrontendOptionsDto
        {
            BaseVolume = value.BaseVolume,
            MinVolume = value.MinVolume,
            MaxVolume = value.MaxVolume,
            ImageSwitchIntervalMs = value.ImageSwitchIntervalMs,
            ImageFadeMs = value.ImageFadeMs
        };
    }

    private static IReadOnlyList<PlaylistDto> ListPlaylists([FromServices] IPlaylistsCollection collection)
    {
        return collection
               .Select(kv => new PlaylistDto { Id = kv.Key, Name = kv.Value.Name, Url = kv.Value.Url })
               .ToList();
    }

    private static IReadOnlyList<SongDto> ListSongs(
        [FromServices] ISongsCollection collection,
        [FromServices] IMediaStorage storage,
        [FromQuery] Guid? playlistId)
    {
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

    private static string GetSongStream(
        [FromServices] IMediaStorage storage,
        long id)
    {
        return storage.GetAudioUrl(id);
    }

    private static IResult GetAudioFile([FromServices] IMediaStorage storage, long id)
    {
        var path = storage.GetAudioPath(id);

        return File.Exists(path)
            ? Results.File(path, "audio/mpeg", enableRangeProcessing: true)
            : Results.NotFound();
    }

    private static IResult GetImageFile([FromServices] IMediaStorage storage, string key)
    {
        var path = storage.GetImagePath(key);

        if (!File.Exists(path))
            return Results.NotFound();

        if (!ContentTypes.TryGetContentType(path, out var contentType))
            contentType = "application/octet-stream";

        return Results.File(path, contentType, enableRangeProcessing: true);
    }

    private static ImagesCountDto ListImages([FromServices] IImagesCollection collection)
    {
        return new ImagesCountDto { Count = collection.Count };
    }

    private static Task<string> GetImageUrl([FromServices] IImagesCollection collection, int index)
    {
        return collection.GetUrl(index);
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
    public required float MinVolume { get; init; }
    public required float MaxVolume { get; init; }
    public required int ImageSwitchIntervalMs { get; init; }
    public required int ImageFadeMs { get; init; }
}