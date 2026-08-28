using FluentAssertions;
using Meta.Audio;
using Xunit;

namespace Tests.Meta.Audio;

public class AudioImportFileNameTests
{
    [Theory]
    [InlineData("1820897418.mp3", 1820897418L)]
    [InlineData("1820897418.MP3", 1820897418L)]
    [InlineData("folder/1820897418.mp3", 1820897418L)]
    [InlineData("  42.mp3  ", 42L)]
    public void ParsesSoundCloudTrackIdFromMp3FileName(string fileName, long expected)
    {
        AudioImportFileName.TryParseSongId(fileName, out var id).Should().BeTrue();
        id.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("track.mp3")]
    [InlineData("0.mp3")]
    [InlineData("-1.mp3")]
    [InlineData("1820897418.m4a")]
    [InlineData("1820897418.mp3.mp3")]
    [InlineData("1820897418")]
    public void RejectsNamesThatAreNotPositiveTrackIdMp3(string? fileName)
    {
        AudioImportFileName.TryParseSongId(fileName, out var id).Should().BeFalse();
        id.Should().Be(0);
    }
}
