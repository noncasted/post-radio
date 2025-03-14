﻿using SoundCloudExplode.Tracks;

namespace Audio;

public class TrackData
{
    public required string DownloadUrl { get; init; }
    public required SongMetadata Metadata { get; init; }
}

public class GetNextTrackRequest
{
    public required int Index { get; init; }
    public required PlaylistType TargetPlaylist { get; init; }
}

[Serializable]
public class SongMetadata
{
    public required string Url { get; init; }
    public required string Author { get; init; }
    public required string Name { get; init; }
    public required string ShortName { get; set; }

    public static SongMetadata Empty => new()
    {
        Url = string.Empty,
        Author = string.Empty,
        Name = string.Empty,
        ShortName = string.Empty
    };

    public override string ToString()
    {
        return $"{Author} - {Name}\n{ShortName}";
    }
}

public static class TrackDataExtensions
{
    public static string ToShortName(this string url)
    {
        return url.Replace("https://soundcloud.com/", "");
    }
    
    public static SongMetadata? ToMetadata(this Track track)
    {
        if (track.Title == null || track.PermalinkUrl == null)
            return null;

        string name = string.Empty;
        string author = string.Empty;

        if (track.Title.Contains(" - "))
        {
            var splitTitle = track.Title.Split(" - ");
            name = splitTitle[0];
            author = splitTitle[1];
        }
        else if (track.Description != null)
        {
            name = track.Title;
            author = track.Description;
        }
        else if (track.PublisherMetadata is { Artist: not null })
        {
            name = track.Title;
            author = track.PublisherMetadata.Artist;
        }

        int duration;

        if (track.Duration != null)
            duration = (int)track.Duration;
        else
            duration = 100000;

        if (duration == 0)
            throw new NullReferenceException();
        
        var url = track.PermalinkUrl.ToString();
        var shortName = url.ToShortName();

        return new SongMetadata()
        {
            Url = url,
            Author = author,
            Name = name,
            ShortName = shortName
        };
    }
}