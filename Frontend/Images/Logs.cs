using Microsoft.Extensions.Logging;

namespace Frontend;

public static partial class Logs
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "[Images] [Refresh] Started")]
    public static partial void ImageRefreshStarted(this ILogger logger);
    
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "[Images] [Refresh] Completed Total images found: {total}")]
    public static partial void ImageRefreshCompleted(this ILogger logger, int total);
}