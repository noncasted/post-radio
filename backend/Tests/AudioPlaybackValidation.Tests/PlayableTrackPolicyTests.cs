using FluentAssertions;
using Frontend.Shared;
using Meta.Audio;
using Xunit;

namespace AudioPlaybackValidation.Tests;

public class PlayableTrackPolicyTests
{
    [Fact]
    public void FrontendAndBackendMinimumPlayableDurationsStayAligned()
    {
        PlayableTrackPolicy.MinimumPlayableDurationMs.Should()
                           .Be((long)AudioTrackValidation.MinimumPlayableDuration.TotalMilliseconds);
    }

    [Theory]
    [InlineData(true, true, 41_000L, true)]
    [InlineData(true, true, 31_000L, true)]
    [InlineData(true, false, 41_000L, false)]
    [InlineData(false, true, 41_000L, false)]
    [InlineData(true, true, 30_000L, false)]
    [InlineData(true, true, null, false)]
    public void BackendPlaybackGateRejectsInvalidOrShortTracks(bool isLoaded, bool isValid, long? durationMs, bool expected)
    {
        AudioTrackValidation.IsPlayableAudio(isLoaded, isValid, durationMs).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true, 41_000L, false)]
    [InlineData(true, true, 31_000L, false)]
    [InlineData(true, false, 41_000L, true)]
    [InlineData(false, true, 41_000L, false)]
    [InlineData(true, true, 30_000L, true)]
    [InlineData(true, true, null, true)]
    [InlineData(false, false, null, true)]
    [InlineData(false, true, null, false)]
    public void InvalidPlaybackCandidateIncludesLoadedShortTracks(
        bool isLoaded,
        bool isValid,
        long? durationMs,
        bool expected)
    {
        AudioTrackValidation.IsInvalidPlaybackCandidate(isLoaded, isValid, durationMs).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, 41_000L, true)]
    [InlineData(true, 31_000L, true)]
    [InlineData(false, 41_000L, false)]
    [InlineData(true, 30_000L, false)]
    [InlineData(true, null, false)]
    public void FrontendPlaybackGateRejectsInvalidOrShortTracks(bool isValid, long? durationMs, bool expected)
    {
        var song = new SongDto
        {
            Id = 1,
            Author = "author",
            Name = "song",
            Url = "https://example.test/song",
            Playlists = Array.Empty<Guid>(),
            AddDate = DateTime.UtcNow,
            DurationMs = durationMs,
            IsValid = isValid
        };

        PlayableTrackPolicy.IsPlayable(song).Should().Be(expected);
    }
}
