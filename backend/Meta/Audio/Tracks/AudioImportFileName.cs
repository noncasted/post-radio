namespace Meta.Audio;

public static class AudioImportFileName
{
    public static bool TryParseSongId(string? fileName, out long id)
    {
        id = 0;
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var name = Path.GetFileName(fileName.Trim());
        if (!name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            return false;

        var stem = Path.GetFileNameWithoutExtension(name);
        if (!long.TryParse(stem, out var parsed) || parsed <= 0)
            return false;

        id = parsed;
        return true;
    }
}
