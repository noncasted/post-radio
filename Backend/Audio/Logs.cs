using Microsoft.Extensions.Logging;

namespace Audio;

public static partial class Logs
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "[Audio] [Refresh] Started")]
    public static partial void AudioRefreshStarted(this ILogger logger);
    
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "[Audio] [Refresh] Completed Total audio found: {total}")]
    public static partial void AudioRefreshCompleted(this ILogger logger, int total);
    
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "[Audio] [Provider] Find audio index: {index}, metadata: {metadata}")]
    public static partial void AudioGetStarted(this ILogger logger, int index, SongMetadata metadata);
    
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "[Audio] [Provider] Audio already cached: index: {index}, metadata: {metadata}")]
    public static partial void AudioAlreadyCached(this ILogger logger, int index, SongMetadata metadata);
    
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "[Audio] [Provider] Audio found in storage: {metadata}")]
    public static partial void AudioFound(this ILogger logger, SongMetadata metadata);
    
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "[Audio] [Provider] Audio not loaded: {metadata}")]
    public static partial void AudioNotLoaded(this ILogger logger, SongMetadata metadata);
}