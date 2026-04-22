using System.IO.Compression;
using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace ConsoleGateway;

public static class ConsoleMediaEndpoints
{
    private const long MaxImageUploadBytes = 250L * 1024L * 1024L;
    private const long MultipartOverheadBytes = 1024L * 1024L;

    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static IEndpointRouteBuilder MapConsoleMediaEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/console/media");

        group.MapPost("/images/upload", UploadImage)
             .DisableAntiforgery()
             .WithMetadata(
                 new RequestSizeLimitAttribute(MaxImageUploadBytes + MultipartOverheadBytes),
                 new RequestFormLimitsAttribute { MultipartBodyLengthLimit = MaxImageUploadBytes + MultipartOverheadBytes });
        group.MapGet("/images/all.zip", DownloadAllImages);
        group.MapGet("/images/selected.zip", DownloadSelectedImages);
        group.MapGet("/images/{key}/download", DownloadImageFile);
        group.MapGet("/images/{key}", GetImageFile);

        return builder;
    }

    private static async Task<IResult> UploadImage([FromServices] IMediaStorage storage, HttpRequest request)
    {
        if (!request.HasFormContentType)
            return Results.BadRequest(new { error = "Expected multipart form data." });

        var form = await request.ReadFormAsync();
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file == null)
            return Results.BadRequest(new { error = "No file was uploaded." });

        if (file.Length <= 0)
            return Results.BadRequest(new { error = "Uploaded file is empty." });

        if (file.Length > MaxImageUploadBytes)
            return Results.BadRequest(new { error = "Uploaded image is too large." });

        var contentType = file.ContentType ?? string.Empty;
        if (!string.IsNullOrEmpty(contentType) &&
            !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
            !contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = $"Uploaded file is not an image. Content-Type: {contentType}" });

        try
        {
            await using var stream = file.OpenReadStream();
            var image = await storage.SaveImage(file.FileName, stream);
            return Results.Ok(new { image.Key, image.FileName, image.SizeBytes, image.LastModifiedUtc });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
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

    private static IResult DownloadSelectedImages([FromServices] IMediaStorage storage, HttpRequest request)
    {
        var keys = request.Query["key"]
                          .Where(key => !string.IsNullOrWhiteSpace(key))
                          .Select(key => key!)
                          .Distinct(StringComparer.Ordinal)
                          .ToList();
        if (keys.Count == 0)
            return Results.BadRequest(new { error = "At least one image key is required." });

        var fileName = $"images-selected-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
        return Results.Stream(stream => WriteSelectedImagesArchive(storage, keys, stream), "application/zip", fileName);
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

    private static async Task WriteSelectedImagesArchive(IMediaStorage storage, IReadOnlyList<string> keys, Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        var entryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in keys)
        {
            var path = storage.GetImagePath(key);
            if (!File.Exists(path))
                continue;

            var entryName = GetUniqueArchiveEntryName(entryNames, Path.GetFileName(path));
            var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
            await using var entryStream = entry.Open();
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 1024 * 128, useAsync: true);
            await file.CopyToAsync(entryStream);
        }
    }

    private static string GetUniqueArchiveEntryName(HashSet<string> entryNames, string fileName)
    {
        if (entryNames.Add(fileName))
            return fileName;

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var suffix = 1; ; suffix++)
        {
            var candidate = $"{baseName}-{suffix}{extension}";
            if (entryNames.Add(candidate))
                return candidate;
        }
    }
}
