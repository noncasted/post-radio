namespace Frontend.Client.Components;

internal enum SetNextResult
{
    None,
    Loaded,
    SkippedMissing,
    TransientFailure,
    NoSong,
    Busy,
    Cancelled
}

internal sealed record LoadAndPlayResult(
    bool Started,
    string? ErrorName,
    string? ErrorMessage,
    int ReadyState,
    int NetworkState,
    int? AudioErrorCode,
    string? AudioErrorMessage);

internal sealed record ResumeResult(
    bool Resumed,
    string? Reason,
    string? ErrorMessage,
    double? BeforeTime,
    bool? BeforePaused,
    double? AfterPlayTime,
    bool? AfterPlayPaused,
    double? CurrentTime,
    double? Duration,
    double? BufferedEnd,
    int? ReadyState,
    int? NetworkState,
    bool? Paused,
    bool? Ended,
    bool? Seeking,
    bool? Nudged,
    double? NudgeFromTime,
    double? NudgeToTime,
    int? ProbeElapsedMs,
    bool? Hidden,
    string? VisibilityState,
    string? UserAgent);

internal sealed record AudioBufferedRange(double? Start, double? End);

internal sealed record AudioStateSnapshot(
    int ReadyState,
    int NetworkState,
    double? CurrentTime,
    double? Duration,
    bool Paused,
    bool Ended,
    bool Muted,
    double? Volume,
    bool Seeking,
    int? ErrorCode,
    string? ErrorMessage,
    string? Src,
    IReadOnlyList<AudioBufferedRange> Buffered,
    bool? Hidden,
    string? VisibilityState,
    string? UserAgent);