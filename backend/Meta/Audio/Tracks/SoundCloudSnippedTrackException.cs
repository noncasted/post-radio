namespace Meta.Audio;

// Thrown when SoundCloud only offers a snipped preview for a track under the
// current session. Distinct from TrackUnavailableException because the track is
// not broken: a session allowed to stream it will download it in full, so the
// song is kept as a retry candidate rather than written off.
public class SoundCloudSnippedTrackException : Exception
{
    public SoundCloudSnippedTrackException(string policy, string diagnostics)
        : base($"SoundCloud served a snipped preview only ({policy})")
    {
        Policy = policy;
        Diagnostics = diagnostics;
    }

    public string Policy { get; }
    public string Diagnostics { get; }
}
