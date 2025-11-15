using System.Diagnostics;

namespace Common;

public static class TraceExtensions
{
    public static readonly ActivitySource PlayerEndpoints = new("Player.Endpoints");
    public static readonly ActivitySource PlayerConnection = new("Player.Connection");
    
    public static readonly IEnumerable<ActivitySource> AllSources =
    [
        PlayerEndpoints,
        PlayerConnection
    ];
    
    public static Activity Start(this ActivitySource source)
    {
        return source.StartActivity(source.Name)!;
    }
    
    public static Activity Start(this ActivitySource source, string name)
    {
        return source.StartActivity(name)!;
    }
}