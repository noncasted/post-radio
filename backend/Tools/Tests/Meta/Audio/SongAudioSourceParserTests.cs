using FluentAssertions;
using Meta.Audio;
using Xunit;

namespace Tests.Meta.Audio;

public class SongAudioSourceParserTests
{
    [Theory]
    [InlineData("youtube", SongAudioSource.YouTube)]
    [InlineData("YouTube", SongAudioSource.YouTube)]
    [InlineData("soundcloud", SongAudioSource.SoundCloud)]
    [InlineData("SoundCloud", SongAudioSource.SoundCloud)]
    [InlineData("", SongAudioSource.Unknown)]
    [InlineData(null, SongAudioSource.Unknown)]
    [InlineData("other", SongAudioSource.Unknown)]
    public void ParsesKnownAudioSources(string? value, SongAudioSource expected)
    {
        SongAudioSourceParser.Parse(value).Should().Be(expected);
    }

    [Fact]
    public void NormalizesYouTubeWatchUrlsAndVideoIds()
    {
        SongAudioSourceParser.NormalizeYouTubeUrl("https://www.youtube.com/watch?v=ufF61Fw6X7E")
                             .Should()
                             .Be("https://www.youtube.com/watch?v=ufF61Fw6X7E");
        SongAudioSourceParser.NormalizeYouTubeUrl("ufF61Fw6X7E")
                             .Should()
                             .Be("https://www.youtube.com/watch?v=ufF61Fw6X7E");
        SongAudioSourceParser.NormalizeYouTubeUrl("  ")
                             .Should()
                             .BeEmpty();
    }
}
