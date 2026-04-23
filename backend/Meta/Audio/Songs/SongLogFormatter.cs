using SoundCloudExplode.Tracks;

namespace Meta.Audio;

public static class SongLogFormatter
{
    public static string FormatLabel(long id, SongState state, Track? track = null)
    {
        var author = FirstNonBlank(state.Author, track?.PublisherMetadata?.Artist);
        var name = FirstNonBlank(state.Name, track?.Title);
        var url = FirstNonBlank(state.Url, track?.PermalinkUrl?.ToString());

        if (author != null && name != null)
            return $"{author} — {name} #{id}";

        if (name != null)
            return $"{name} #{id} (missing author)";

        if (author != null)
            return $"{author} #{id} (missing title)";

        if (url != null)
            return $"track #{id} (missing author/title, url={url})";

        return $"track #{id} (missing author/title/url)";
    }

    private static string? FirstNonBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }
}
