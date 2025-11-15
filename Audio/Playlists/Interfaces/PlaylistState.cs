namespace Audio;

[GenerateSerializer]
public class PlaylistState
{
    [Id(0)] public string Name { get; set; }
    [Id(1)] public string Url { get; set; }
}