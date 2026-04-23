using System.Globalization;
using Frontend.Shared;

namespace Frontend.Client.Components;

internal static class AudioPlayerFormatters
{
    public static string? FormatSongLabel(SongDto? song)
    {
        if (song == null)
            return null;

        var author = song.Author?.Trim();
        var name = song.Name?.Trim();

        if (string.IsNullOrEmpty(author) && string.IsNullOrEmpty(name))
            return $"#{song.Id}";

        if (string.IsNullOrEmpty(author))
            return $"{name} #{song.Id}";

        if (string.IsNullOrEmpty(name))
            return $"{author} #{song.Id}";

        return $"{author} — {name} #{song.Id}";
    }

    public static string FormatNumber(double? value)
    {
        if (!value.HasValue)
            return "-";

        return value.Value.ToString("F2", CultureInfo.InvariantCulture);
    }

    public static string ReadyStateName(int value) => value switch
    {
        0 => "HAVE_NOTHING",
        1 => "HAVE_METADATA",
        2 => "HAVE_CURRENT_DATA",
        3 => "HAVE_FUTURE_DATA",
        4 => "HAVE_ENOUGH_DATA",
        _ => "?"
    };

    public static string NetworkStateName(int value) => value switch
    {
        0 => "NETWORK_EMPTY",
        1 => "NETWORK_IDLE",
        2 => "NETWORK_LOADING",
        3 => "NETWORK_NO_SOURCE",
        _ => "?"
    };

    public static string MediaErrorName(int value) => value switch
    {
        1 => "MEDIA_ERR_ABORTED",
        2 => "MEDIA_ERR_NETWORK",
        3 => "MEDIA_ERR_DECODE",
        4 => "MEDIA_ERR_SRC_NOT_SUPPORTED",
        _ => "?"
    };
}