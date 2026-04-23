using FluentAssertions;
using Meta.Audio;
using Xunit;

namespace Tests.Meta.Audio;

public class SongMetadataMergeTests
{
    [Fact]
    public void LookupMergePreservesExistingDurationWhenCacheDurationIsMissing()
    {
        var existing = new SongState
        {
            Url = "https://soundcloud.example/original",
            Author = "Unknown",
            Name = "Unknown",
            AddDate = new DateTime(2024, 01, 02, 03, 04, 05, DateTimeKind.Utc),
            IsLoaded = true,
            DurationMs = 180_000,
            IsValid = false
        };
        var lookup = new SongLookupInfo
        {
            Id = 1,
            Url = "https://soundcloud.example/cached",
            Author = "Cached author",
            Name = "Cached title"
        };

        var merged = SongMetadataMerge.MergeLookup(1, existing, lookup);

        merged.Author.Should().Be("Cached author");
        merged.Name.Should().Be("Cached title");
        merged.DurationMs.Should().Be(180_000);
        merged.IsLoaded.Should().BeTrue();
        merged.IsValid.Should().BeTrue();
    }

    [Fact]
    public void LookupMergeDoesNotRestoreLoadedTrackWithInvalidShortDuration()
    {
        var existing = new SongState
        {
            Url = "https://soundcloud.example/original",
            Author = "Unknown",
            Name = "Unknown",
            IsLoaded = true,
            DurationMs = 30_000,
            IsValid = false
        };
        var lookup = new SongLookupInfo
        {
            Id = 1,
            Url = "https://soundcloud.example/cached",
            Author = "Cached author",
            Name = "Cached title"
        };

        var merged = SongMetadataMerge.MergeLookup(1, existing, lookup);

        merged.Author.Should().Be("Cached author");
        merged.Name.Should().Be("Cached title");
        merged.DurationMs.Should().Be(30_000);
        merged.IsValid.Should().BeFalse();
    }

    [Fact]
    public void FetchMergeKeepsCachedMetadataWhenFetchedAuthorIsMissing()
    {
        var playlistId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var existing = new SongState
        {
            Url = "https://soundcloud.example/original",
            Playlists = new List<Guid> { playlistId },
            Author = "Unknown",
            Name = "Unknown",
            AddDate = new DateTime(2024, 01, 02, 03, 04, 05, DateTimeKind.Utc),
            IsLoaded = true,
            DurationMs = 120_000,
            IsValid = false
        };
        var cached = new SongLookupInfo
        {
            Id = 1,
            Url = "https://soundcloud.example/cached",
            Author = "Cached author",
            Name = "Cached title"
        };

        var merged = SongMetadataMerge.MergeFetchedExisting(
            1,
            existing,
            cached,
            playlistId,
            "https://soundcloud.example/fetched",
            string.Empty,
            "Unknown",
            TimeSpan.FromMinutes(2),
            new DateTime(2025, 01, 02, 03, 04, 05, DateTimeKind.Utc));

        merged.Author.Should().Be("Cached author");
        merged.Name.Should().Be("Cached title");
        merged.DurationMs.Should().Be(120_000);
        merged.IsValid.Should().BeTrue();
    }

    [Fact]
    public void FetchMergeUsesCacheMetadataOverExistingAndFetchedData()
    {
        var playlistId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var existing = new SongState
        {
            Url = "https://soundcloud.example/original",
            Playlists = new List<Guid> { playlistId },
            Author = "Manual author",
            Name = "Manual title",
            AddDate = new DateTime(2024, 01, 02, 03, 04, 05, DateTimeKind.Utc),
            IsLoaded = true,
            DurationMs = 180_000,
            IsValid = true
        };
        var cached = new SongLookupInfo
        {
            Id = 1,
            Url = "https://soundcloud.example/cached",
            Author = "Cached author",
            Name = "Cached title"
        };

        var merged = SongMetadataMerge.MergeFetchedExisting(
            1,
            existing,
            cached,
            playlistId,
            "https://soundcloud.example/fetched",
            "Fetched author",
            "Fetched title",
            null,
            new DateTime(2025, 01, 02, 03, 04, 05, DateTimeKind.Utc));

        merged.Author.Should().Be("Cached author");
        merged.Name.Should().Be("Cached title");
        merged.DurationMs.Should().Be(180_000);
        merged.IsValid.Should().BeTrue();
    }
}
