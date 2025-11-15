namespace Audio;

[GenerateSerializer]
public class PlaylistData
{
    [Id(0)] public required Guid Id { get; init; }
    [Id(1)] public required string Url { get; init; }
    [Id(2)] public required string Name { get; init; }
}