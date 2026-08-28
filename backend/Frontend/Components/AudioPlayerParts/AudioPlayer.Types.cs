namespace Frontend.Components;

/// <summary>Track handed to the JS player. Token correlates JS callbacks back to a <see cref="Frontend.Shared.SongDto"/>.</summary>
internal sealed record QueuedTrack(long Token, long SongId, string Url);

public sealed record ResumeAttemptInfo(
    int Attempt,
    bool Resumed,
    string? Detail,
    string? ErrorMessage,
    double? BeforeTime,
    double? AfterTime,
    bool Nudged,
    int ElapsedMs);

public sealed record AudioEventInfo(
    string Name,
    double AtSeconds,
    double? CurrentTime,
    int ReadyState,
    int NetworkState);

/// <summary>
/// Everything the JS player observed while a track was playing. Sent once per track
/// together with the finish reason, so collapsing the interop does not lose telemetry.
/// </summary>
public sealed record TrackDiagnostics(
    double? CurrentTime,
    double? Duration,
    double? BufferedEnd,
    int ReadyState,
    int NetworkState,
    bool Paused,
    bool Ended,
    bool Seeking,
    int? ErrorCode,
    string? ErrorMessage,
    string? PlayErrorName,
    string? PlayErrorMessage,
    int BufferingCount,
    double BufferingTotalSeconds,
    double SinceStartSeconds,
    double SinceProgressSeconds,
    bool Hidden,
    string? VisibilityState,
    double HiddenSeconds,
    IReadOnlyList<ResumeAttemptInfo>? ResumeAttempts,
    IReadOnlyList<AudioEventInfo>? Events,
    string? UserAgent);
