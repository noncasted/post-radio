namespace Audio;

[GenerateSerializer]
public class SongState
{
    [Id(0)] public string Url { get; set; }
    [Id(1)] public List<Guid> Playlists { get; set; } = new();
    [Id(2)] public string Author { get; set; }
    [Id(3)] public string Name { get; set; }
    [Id(4)] public DateTime AddDate { get; set; }
}