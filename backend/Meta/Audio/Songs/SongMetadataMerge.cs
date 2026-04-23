namespace Meta.Audio;

public static class SongMetadataMerge
{
    public static SongData MergeLookup(long id, SongState existing, SongLookupInfo lookup)
    {
        var durationMs = existing.DurationMs ?? lookup.DurationMs;
        var author = ChooseAuthor(lookup.Author, existing.Author);
        var name = ChooseName(lookup.Name, existing.Name);

        return new SongData
        {
            Id = id,
            Url = ChooseUrl(existing.Url, lookup.Url),
            Playlists = existing.Playlists,
            Author = author,
            Name = name,
            AddDate = existing.AddDate,
            IsLoaded = existing.IsLoaded,
            DurationMs = durationMs,
            IsValid = MergeLookupValidity(existing, lookup, author, durationMs)
        };
    }

    public static SongData MergeFetchedExisting(
        long id,
        SongState existing,
        SongLookupInfo? cached,
        Guid playlistId,
        string trackUrl,
        string trackAuthor,
        string trackName,
        TimeSpan? localDuration,
        DateTime now)
    {
        var playlists = existing.Playlists.Contains(playlistId)
            ? existing.Playlists
            : existing.Playlists.Concat(new[] { playlistId }).ToList();
        var addDate = existing.Playlists.Contains(playlistId)
            ? existing.AddDate
            : now;
        var durationMs = PlaylistLoader.ToDurationMs(localDuration) ?? existing.DurationMs ?? cached?.DurationMs;
        var author = ChooseAuthor(cached?.Author, existing.Author, trackAuthor);
        var name = ChooseName(cached?.Name, existing.Name, trackName);

        return new SongData
        {
            Id = id,
            Url = ChooseUrl(trackUrl, existing.Url, cached?.Url),
            Playlists = playlists,
            Author = author,
            Name = name,
            AddDate = addDate,
            IsLoaded = existing.IsLoaded,
            DurationMs = durationMs,
            IsValid = MergeFetchValidity(existing, cached, author, localDuration, durationMs)
        };
    }

    public static SongData MergeFetchedNew(
        long id,
        SongLookupInfo? cached,
        Guid playlistId,
        string trackUrl,
        string trackAuthor,
        string trackName,
        TimeSpan? localDuration,
        DateTime now)
    {
        var durationMs = PlaylistLoader.ToDurationMs(localDuration) ?? cached?.DurationMs;
        var author = ChooseAuthor(cached?.Author, trackAuthor);
        var name = ChooseName(cached?.Name, trackName);

        return new SongData
        {
            Id = id,
            Url = ChooseUrl(trackUrl, cached?.Url),
            Playlists = new[] { playlistId },
            Author = author,
            Name = name,
            AddDate = now,
            IsLoaded = false,
            DurationMs = durationMs,
            IsValid = MergeNewFetchValidity(cached, author, localDuration, durationMs)
        };
    }

    public static bool HasChanges(SongState existing, SongData data)
    {
        return existing.Url != data.Url
               || existing.Author != data.Author
               || existing.Name != data.Name
               || !existing.Playlists.SequenceEqual(data.Playlists)
               || existing.AddDate != data.AddDate
               || existing.IsLoaded != data.IsLoaded
               || existing.DurationMs != data.DurationMs
               || existing.IsValid != data.IsValid;
    }

    private static bool MergeLookupValidity(SongState existing, SongLookupInfo lookup, string author, long? durationMs)
    {
        if (!IsUsefulAuthor(author))
            return false;

        if (HasInvalidLoadedDuration(existing.IsLoaded, durationMs))
            return false;

        if (lookup.IsValid.HasValue)
            return lookup.IsValid.Value;

        if (existing.IsValid)
            return true;

        return !IsUsefulAuthor(existing.Author);
    }

    private static bool MergeFetchValidity(
        SongState existing,
        SongLookupInfo? cached,
        string author,
        TimeSpan? localDuration,
        long? durationMs)
    {
        if (!IsUsefulAuthor(author))
            return false;

        if (localDuration.HasValue)
            return AudioTrackValidation.IsValidLocalDuration(localDuration);

        if (HasInvalidLoadedDuration(existing.IsLoaded, durationMs))
            return false;

        if (existing.IsValid)
            return true;

        if (!IsUsefulAuthor(existing.Author))
            return cached?.IsValid ?? true;

        return false;
    }

    private static bool MergeNewFetchValidity(
        SongLookupInfo? cached,
        string author,
        TimeSpan? localDuration,
        long? durationMs)
    {
        if (!IsUsefulAuthor(author))
            return false;

        if (localDuration.HasValue)
            return AudioTrackValidation.IsValidLocalDuration(localDuration);

        if (durationMs.HasValue && !AudioTrackValidation.IsValidLocalDurationMs(durationMs))
            return false;

        return cached?.IsValid ?? true;
    }

    private static string ChooseAuthor(params string?[] values)
    {
        return Choose(values, IsUsefulAuthor);
    }

    private static string ChooseName(params string?[] values)
    {
        return Choose(values, IsUsefulName);
    }

    private static string ChooseUrl(params string?[] values)
    {
        return Choose(values, value => !string.IsNullOrWhiteSpace(value));
    }

    private static string Choose(string?[] values, Func<string, bool> isUseful)
    {
        foreach (var value in values)
        {
            if (value != null && isUseful(value))
                return value;
        }

        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static bool IsUsefulAuthor(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && !string.Equals(value.Trim(), "unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUsefulName(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && !string.Equals(value.Trim(), "unknown", StringComparison.OrdinalIgnoreCase)
               && !string.Equals(value.Trim(), "untitled", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasInvalidLoadedDuration(bool isLoaded, long? durationMs)
    {
        return isLoaded && !AudioTrackValidation.IsValidLocalDurationMs(durationMs);
    }
}
