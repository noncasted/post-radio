using SoundCloudExplode.Tracks;

namespace Meta.Audio;

// SoundCloud reports per-session streaming rights through the track "policy"
// field. SNIP means the current session is only allowed a 30 second preview:
// every transcoding comes back with snipped=true and duration=30000, while
// full_duration still holds the real length. The verdict depends on the session
// (region, authorization), not on the track itself, so it must not be treated
// as a permanent defect.
public static class SoundCloudTrackPolicy
{
    public const string Snip = "SNIP";
    public const string Allow = "ALLOW";
    public const string Block = "BLOCK";

    public static bool IsSnip(string? policy)
    {
        return string.Equals(policy, Snip, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSnippedTrack(Track? track)
    {
        if (track == null)
            return false;

        if (IsSnip(track.Policy))
            return true;

        var transcodings = track.Media?.Transcodings;
        if (transcodings == null || transcodings.Count == 0)
            return false;

        return transcodings.All(transcoding => transcoding.Snipped);
    }

    public static string Describe(Track? track)
    {
        if (track == null)
            return "policy=unknown";

        var policy = string.IsNullOrWhiteSpace(track.Policy) ? "unknown" : track.Policy;
        var monetization = string.IsNullOrWhiteSpace(track.MonetizationModel) ? "unknown" : track.MonetizationModel;

        return $"policy={policy}, monetization={monetization}";
    }
}
