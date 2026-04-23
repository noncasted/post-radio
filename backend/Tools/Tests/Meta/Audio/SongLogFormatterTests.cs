using FluentAssertions;
using Meta.Audio;
using Xunit;

namespace Tests.Meta.Audio;

public class SongLogFormatterTests
{
    [Fact]
    public void MissingMetadataLabelUsesExplicitMissingFieldsInsteadOfUnknown()
    {
        var state = new SongState
        {
            Url = "https://soundcloud.example/track"
        };

        SongLogFormatter.FormatLabel(123, state).Should()
                        .Be("track #123 (missing author/title, url=https://soundcloud.example/track)");
    }

    [Fact]
    public void PartialMetadataLabelKeepsKnownTitleAndMissingAuthor()
    {
        var state = new SongState
        {
            Name = "Known title"
        };

        SongLogFormatter.FormatLabel(123, state).Should()
                        .Be("Known title #123 (missing author)");
    }
}
