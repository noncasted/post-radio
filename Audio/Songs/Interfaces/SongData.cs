namespace Audio;

[GenerateSerializer]
public class SongData
{
    [Id(0)] public required long Id { get; init; }
    [Id(1)] public required IReadOnlyList<Guid> Playlists { get; init; }
    [Id(2)] public required string Url { get; init; }
    [Id(3)] public required string Author { get; init; }
    [Id(4)] public required string Name { get; init; }
    [Id(5)] public required DateTime AddDate { get; init; }
}