using Microsoft.Extensions.Logging;
using Orleans.Runtime.Configuration;

namespace Infrastructure.Orleans;

public static partial class GrainStorageLogs
{
    [LoggerMessage(
        EventId = 8000,
        Level = LogLevel.Trace,
        Message =
            "Clearing grain state: ProviderName={Name} GrainType={BaseGrainType} GrainId={GrainId} ETag={ETag}."
    )]
    public static partial void LogTraceClearingGrainState(
        this ILogger logger,
         string name, string baseGrainType, GrainKey grainId, string etag);

    [LoggerMessage(
        EventId = 8001,
        Level = LogLevel.Error,
        Message =
            "Error clearing grain state: ProviderName={Name} GrainType={BaseGrainType} GrainId={GrainId} ETag={ETag}."
    )]
    public static partial void LogErrorClearingGrainState(this ILogger logger,
        Exception exception,  string name, string baseGrainType, GrainKey grainId, string etag);

    [LoggerMessage(
        EventId = 8002,
        Level = LogLevel.Trace,
        Message =
            "Cleared grain state: ProviderName={Name} GrainType={BaseGrainType} GrainId={GrainId} ETag={ETag}."
    )]
    public static partial void LogTraceClearedGrainState(this ILogger logger,
         string name, string baseGrainType, GrainKey grainId, string etag);

    [LoggerMessage(
        EventId = 8003,
        Level = LogLevel.Trace,
        Message =
            "Reading grain state: ProviderName={Name} GrainType={BaseGrainType} GrainId={GrainId} ETag={ETag}."
    )]
    public static partial void LogTraceReadingGrainState(this ILogger logger,
         string name, string baseGrainType, GrainKey grainId, string etag);

    [LoggerMessage(
        EventId = 8004,
        Level = LogLevel.Trace,
        Message =
            "Null grain state read (default will be instantiated): ProviderName={Name} GrainType={BaseGrainType} GrainId={GrainId} ETag={ETag}."
    )]
    public static partial void LogTraceNullGrainStateRead(this ILogger logger,
         string name, string baseGrainType, GrainKey grainId, string etag);

    [LoggerMessage(
        EventId = 8005,
        Level = LogLevel.Trace,
        Message =
            "Read grain state: ProviderName={Name} GrainType={BaseGrainType} GrainId={GrainId} ETag={ETag}."
    )]
    public static partial void LogTraceReadGrainState(this ILogger logger,
         string name, string baseGrainType, GrainKey grainId, string etag);

    [LoggerMessage(
        EventId = 8006,
        Level = LogLevel.Error,
        Message =
            "Error reading grain state: ProviderName={Name} GrainType={BaseGrainType} GrainId={GrainId} ETag={ETag}."
    )]
    public static partial void LogErrorReadingGrainState(this ILogger logger,
        Exception exception,  string name, string baseGrainType, GrainKey grainId, string etag);

    [LoggerMessage(
        EventId = 8007,
        Level = LogLevel.Trace,
        Message =
            "Writing grain state: ProviderName={Name} GrainType={BaseGrainType} GrainId={GrainId} ETag={ETag}."
    )]
    public static partial void LogTraceWritingGrainState(this ILogger logger,
         string name, string baseGrainType, GrainKey grainId, string etag);

    [LoggerMessage(
        EventId = 8008,
        Level = LogLevel.Error,
        Message =
            "Error writing grain state: ProviderName={Name} GrainType={BaseGrainType} GrainId={GrainId} ETag={ETag}."
    )]
    public static partial void LogErrorWritingGrainState(this ILogger logger,
        Exception exception,  string name, string baseGrainType, GrainKey grainId, string etag);

    [LoggerMessage(
        EventId = 8009,
        Level = LogLevel.Trace,
        Message =
            "Wrote grain state: ProviderName={Name} GrainType={BaseGrainType} GrainId={GrainId} ETag={ETag}."
    )]
    public static partial void LogTraceWroteGrainState(this ILogger logger,
         string name, string baseGrainType, GrainKey grainId, string etag);

    [LoggerMessage(
        EventId = 8010,
        Level = LogLevel.Information,
        Message =
            "Initialized storage provider: ProviderName={Name} Invariant={InvariantName} ConnectionString={ConnectionString}."
    )]
    public static partial void LogInfoInitializedStorageProvider(this ILogger logger,
         string name, string invariantName, ConnectionStringLogRecord connectionString);

    public readonly struct ConnectionStringLogRecord(string connectionString)
    {
        public override string ToString() => ConfigUtilities.RedactConnectionStringInfo(connectionString);
    }
}