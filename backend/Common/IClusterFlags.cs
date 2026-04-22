namespace Common;

public interface IClusterFlags
{
    bool MatchmakingEnabled { get; }
    bool SideEffectsEnabled { get; }
    bool SnapshotDiffGuardEnabled { get; }
}