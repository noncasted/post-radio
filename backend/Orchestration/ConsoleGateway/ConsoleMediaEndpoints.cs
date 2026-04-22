using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace ConsoleGateway;

public static class ConsoleMediaEndpoints
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static IEndpointRouteBuilder MapConsoleMediaEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/console/media");

        group.MapGet("/images/all.zip", DownloadAllImages);
        group.MapGet("/images/{key}/download", DownloadImageFile);
        group.MapGet("/images/{key}", GetImageFile);

        return builder;
    }

    private static IResult GetImageFile([FromServices] IMediaStorage storage, string key)
    {
        return GetImageResult(storage, key, download: false);
    }

    private static IResult DownloadImageFile([FromServices] IMediaStorage storage, string key)
    {
        return GetImageResult(storage, key, download: true);
    }

    private static IResult DownloadAllImages([FromServices] IMediaStorage storage)
    {
        var fileName = $"images-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
        return Results.Stream(stream => storage.WriteImagesArchive(stream), "application/zip", fileName);
    }

    private static IResult GetImageResult(IMediaStorage storage, string key, bool download)
    {
        var path = storage.GetImagePath(key);
        if (!File.Exists(path))
            return Results.NotFound();

        if (!ContentTypes.TryGetContentType(path, out var contentType))
            contentType = "application/octet-stream";

        var fileName = download ? Path.GetFileName(path) : null;
        return Results.File(path, contentType, fileName, enableRangeProcessing: true);
    }
}
