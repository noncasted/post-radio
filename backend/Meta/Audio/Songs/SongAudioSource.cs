namespace Meta.Audio;

[GenerateSerializer]
public enum SongAudioSource
{
    Unknown = 0,
    SoundCloud = 1,
    YouTube = 2
}

public static class SongAudioSourceParser
{
    public static SongAudioSource Parse(string? value)
    {
        if (string.Equals(value, "youtube", StringComparison.OrdinalIgnoreCase))
            return SongAudioSource.YouTube;

        if (string.Equals(value, "soundcloud", StringComparison.OrdinalIgnoreCase))
            return SongAudioSource.SoundCloud;

        return SongAudioSource.Unknown;
    }

    public static string NormalizeYouTubeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (trimmed.StartsWith("https://www.youtube.com/watch?", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://youtube.com/watch?", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://youtu.be/", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (trimmed.Length is >= 8 and <= 16
            && trimmed.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-'))
        {
            return $"https://www.youtube.com/watch?v={trimmed}";
        }

        return trimmed;
    }
}
