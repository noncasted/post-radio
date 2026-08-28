using System.IO.Compression;
using Common;
using Meta.Audio;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace ConsoleGateway;

public static class ConsoleMediaEndpoints
{
    private const long MaxImageUploadBytes = 250L * 1024L * 1024L;
    private const long MaxAudioUploadBytes = 80L * 1024L * 1024L;
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

        group.MapGet("/audio/missing", ListMissingAudio);
        group.MapPost("/audio/upload", UploadAudio)
             .DisableAntiforgery()
             .WithMetadata(
                 new RequestSizeLimitAttribute(MaxAudioUploadBytes + MultipartOverheadBytes),
                 new RequestFormLimitsAttribute { MultipartBodyLengthLimit = MaxAudioUploadBytes + MultipartOverheadBytes });

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

    private static IResult ListMissingAudio([FromServices] ISongsCollection songs)
    {
        var missing = songs
            .Where(kv => AudioTrackValidation.IsLoadCandidate(kv.Value.IsLoaded, kv.Value.IsValid, kv.Value.DurationMs))
            .Select(kv => new
            {
                Id = kv.Key,
                Author = kv.Value.Author,
                Name = kv.Value.Name,
                Url = kv.Value.Url,
                DurationMs = kv.Value.DurationMs,
                IsLoaded = kv.Value.IsLoaded,
                IsValid = kv.Value.IsValid,
                IsSnipped = kv.Value.IsSnipped,
                AudioSource = kv.Value.AudioSource.ToString(),
                YouTubeUrl = kv.Value.YouTubeUrl
            })
            .OrderBy(song => song.Author, StringComparer.OrdinalIgnoreCase)
            .ThenBy(song => song.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Results.Ok(missing);
    }

    private static async Task<IResult> UploadAudio(
        [FromServices] IPlaylistLoader loader,
        HttpRequest request)
    {
        if (!request.HasFormContentType)
            return Results.BadRequest(new { error = "Expected multipart form data." });

        var form = await request.ReadFormAsync();
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file == null)
            return Results.BadRequest(new { error = "No file was uploaded." });

        if (file.Length <= 0)
            return Results.BadRequest(new { error = "Uploaded file is empty." });

        if (file.Length > MaxAudioUploadBytes)
            return Results.BadRequest(new { error = "Uploaded audio is too large." });

        var contentType = file.ContentType ?? string.Empty;
        if (!string.IsNullOrEmpty(contentType) &&
            !contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) &&
            !contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = $"Uploaded file is not audio. Content-Type: {contentType}" });

        if (!TryResolveSongId(form, file, out var songId, out var idError))
            return Results.BadRequest(new { error = idError });

        var source = SongAudioSourceParser.Parse(form["source"].ToString());
        var youTubeUrl = SongAudioSourceParser.NormalizeYouTubeUrl(form["youtubeUrl"].ToString());

        try
        {
            await using var stream = file.OpenReadStream();
            var imported = await loader.ImportAudio(songId, stream, source, youTubeUrl);
            return Results.Ok(new
            {
                imported.Id,
                imported.Author,
                imported.Name,
                imported.DurationMs,
                imported.IsValid,
                AudioSource = imported.AudioSource.ToString(),
                imported.YouTubeUrl,
                SizeBytes = file.Length
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static bool TryResolveSongId(IFormCollection form, IFormFile file, out long songId, out string error)
    {
        songId = 0;
        error = string.Empty;

        var idValue = form["id"].ToString();
        if (!string.IsNullOrWhiteSpace(idValue))
        {
            if (long.TryParse(idValue, out songId) && songId > 0)
                return true;

            error = $"Invalid song id: {idValue}";
            return false;
        }

        if (AudioImportFileName.TryParseSongId(file.FileName, out songId))
            return true;

        error = "File name must be {id}.mp3, for example 1820897418.mp3.";
        return false;
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
