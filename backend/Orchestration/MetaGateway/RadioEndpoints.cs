using System.Text.Json;
using Cluster.Configs;
using Common;
using Meta.Audio;
using Meta.Images;
using Meta.Online;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;

namespace MetaGateway;

internal sealed class RadioSkipLog;

public static class RadioEndpoints
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();
    private static readonly TimeSpan DuplicateEmptyUrlSkipLogWindow = TimeSpan.FromSeconds(30);
    private static readonly Dictionary<string, SkipLogRateLimitEntry> EmptyUrlSkipLogRateLimits = new();
    private static readonly object EmptyUrlSkipLogRateLimitLock = new();
    private const int MaxEmptyUrlSkipLogRateLimitEntries = 1024;

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
        group.MapPost("/presence/beat", ReceivePresenceBeat);
        group.MapPost("/skip-report", ReceiveSkipReport);

        return builder;
    }

    private static FrontendOptionsDto GetOptions(
        [FromServices] IFrontendConfig config)
    {
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
        [FromServices] IPlaylistsCollection collection)
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
               .Where(kv => AudioTrackValidation.IsPlayableAudio(
                                kv.Value.IsLoaded,
                                kv.Value.IsValid,
                                kv.Value.DurationMs)
                            && File.Exists(storage.GetAudioPath(kv.Key)))
               .Select(kv => new SongDto
               {
                   Id = kv.Key,
                   Author = kv.Value.Author,
                   Name = kv.Value.Name,
                   Url = kv.Value.Url,
                   Playlists = kv.Value.Playlists,
                   AddDate = kv.Value.AddDate,
                   DurationMs = kv.Value.DurationMs,
                   IsValid = kv.Value.IsValid
               })
               .ToList();
    }

    private static IResult GetSongStream(
        [FromServices] IMediaStorage storage,
        [FromServices] ISongsCollection songs,
        long id)
    {
        if (!songs.TryGetValue(id, out var song)
            || !AudioTrackValidation.IsPlayableAudio(song.IsLoaded, song.IsValid, song.DurationMs))
            return Results.NotFound();

        if (!File.Exists(storage.GetAudioPath(id)))
            return Results.NotFound();

        return Results.Text(storage.GetAudioUrl(id), "text/plain");
    }

    private static IResult GetAudioFile(
        [FromServices] IMediaStorage storage,
        [FromServices] ISongsCollection songs,
        long id)
    {
        if (!songs.TryGetValue(id, out var song)
            || !AudioTrackValidation.IsPlayableAudio(song.IsLoaded, song.IsValid, song.DurationMs))
            return Results.NotFound();

        var path = storage.GetAudioPath(id);

        if (!File.Exists(path))
            return Results.NotFound();

        return Results.File(path, "audio/mpeg", enableRangeProcessing: true);
    }

    private static IResult GetImageFile(
        [FromServices] IMediaStorage storage,
        string key)
    {
        var path = storage.GetImagePath(key);

        if (!File.Exists(path))
            return Results.NotFound();

        if (!ContentTypes.TryGetContentType(path, out var contentType))
            contentType = "application/octet-stream";

        return Results.File(path, contentType, enableRangeProcessing: true);
    }

    private static ImagesCountDto ListImages(
        [FromServices] IImagesCollection collection)
    {
        return new ImagesCountDto { Count = collection.Count };
    }

    private static async Task<string> GetImageUrl(
        [FromServices] IImagesCollection collection,
        int index)
    {
        return await collection.GetUrl(index);
    }

    /// <summary>
    /// The only presence signal there is. The browser posts it every 30 seconds while audio is
    /// actually playing, identified by the fingerprint in <c>X-Radio-Client-Id</c>; a request
    /// without one is not counted, so crawlers and page views never show up as listeners.
    /// </summary>
    private static IResult ReceivePresenceBeat(
        [FromServices] IOnlineTracker onlineTracker,
        HttpContext context)
    {
        onlineTracker.Touch(GetClientId(context));
        return Results.NoContent();
    }

    private static IResult ReceiveSkipReport(
        [FromServices] ILogger<RadioSkipLog> logger,
        [FromBody] JsonElement report,
        HttpContext context)
    {
        var sessionId = TryGetString(report, "sessionId") ?? "-";
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "-";
        var reason = TryGetString(report, "reason") ?? "-";
        var sourceReason = TryGetString(report, "sourceReason") ?? "-";
        var severity = TryGetString(report, "severity") ?? "-";
        var title = TryGetString(report, "title") ?? "-";
        var songId = TryGetString(report, "songId") ?? "-";
        var songLabel = TryGetString(report, "songLabel") ?? "-";
        var candidateSongId = TryGetString(report, "candidateSongId") ?? "-";
        var candidateSongLabel = TryGetString(report, "candidateSongLabel") ?? "-";
        var streamStatusCode = TryGetString(report, "streamStatusCode") ?? "-";
        var streamIsNotFound = TryGetString(report, "streamIsNotFound") ?? "-";
        var rawJson = report.GetRawText();
        var now = DateTime.UtcNow;
        var suppressedCount = GetSuppressedEmptyUrlSkipLogCount(
            sessionId,
            remoteIp,
            severity,
            reason,
            sourceReason,
            title,
            songId,
            songLabel,
            candidateSongId,
            candidateSongLabel,
            streamStatusCode,
            streamIsNotFound,
            userAgent,
            now,
            out var shouldLog);

        if (!shouldLog)
            return Results.NoContent();

        var level = severity switch
        {
            "error" => LogLevel.Error,
            "warning" => LogLevel.Warning,
            _ => LogLevel.Information
        };

        logger.Log(
            level,
            "[RadioSkip] session={SessionId} remote={RemoteIp} severity={Severity} reason={Reason} sourceReason={SourceReason} title={Title} songId={SongId} song={SongLabel} candidateSongId={CandidateSongId} candidateSong={CandidateSongLabel} streamStatus={StreamStatusCode} streamNotFound={StreamIsNotFound} userAgent={UserAgent} suppressedDuplicates={SuppressedDuplicates} payload={Payload}",
            sessionId, remoteIp, severity, reason, sourceReason, title, songId, songLabel, candidateSongId,
            candidateSongLabel, streamStatusCode, streamIsNotFound, userAgent, suppressedCount, rawJson);

        return Results.NoContent();
    }

    private static int GetSuppressedEmptyUrlSkipLogCount(
        string? sessionId,
        string remoteIp,
        string severity,
        string reason,
        string sourceReason,
        string title,
        string songId,
        string songLabel,
        string candidateSongId,
        string candidateSongLabel,
        string streamStatusCode,
        string streamIsNotFound,
        string userAgent,
        DateTime now,
        out bool shouldLog)
    {
        shouldLog = true;

        if (reason != "empty-url")
            return 0;

        var key = string.Join('\u001f',
            sessionId ?? "-",
            remoteIp,
            severity,
            reason,
            sourceReason,
            title,
            songId,
            songLabel,
            candidateSongId,
            candidateSongLabel,
            streamStatusCode,
            streamIsNotFound,
            userAgent);

        lock (EmptyUrlSkipLogRateLimitLock)
        {
            PruneEmptyUrlSkipLogRateLimits(now);

            if (EmptyUrlSkipLogRateLimits.TryGetValue(key, out var entry))
            {
                if (now - entry.LastLoggedUtc < DuplicateEmptyUrlSkipLogWindow)
                {
                    entry.SuppressedCount++;
                    shouldLog = false;
                    return entry.SuppressedCount;
                }

                var suppressedCount = entry.SuppressedCount;
                entry.LastLoggedUtc = now;
                entry.SuppressedCount = 0;
                return suppressedCount;
            }

            EmptyUrlSkipLogRateLimits[key] = new SkipLogRateLimitEntry(now);
            return 0;
        }
    }

    private static void PruneEmptyUrlSkipLogRateLimits(DateTime now)
    {
        if (EmptyUrlSkipLogRateLimits.Count <= MaxEmptyUrlSkipLogRateLimitEntries)
            return;

        foreach (var (key, entry) in EmptyUrlSkipLogRateLimits.ToArray())
        {
            if (now - entry.LastLoggedUtc >= DuplicateEmptyUrlSkipLogWindow)
                EmptyUrlSkipLogRateLimits.Remove(key);
        }
    }

    private sealed class SkipLogRateLimitEntry(DateTime lastLoggedUtc)
    {
        public DateTime LastLoggedUtc { get; set; } = lastLoggedUtc;
        public int SuppressedCount { get; set; }
    }

    private static string? TryGetString(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        if (!root.TryGetProperty(name, out var property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => property.ToString()
        };
    }

    private static string? GetClientId(HttpContext context)
    {
        return context.Request.Headers.TryGetValue("X-Radio-Client-Id", out var clientId)
            ? clientId.ToString()
            : null;
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
    public long? DurationMs { get; init; }
    public bool IsValid { get; init; } = true;
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
