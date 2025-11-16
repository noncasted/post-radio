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

    extension(ActivitySource source)
    {
        public Activity Start()
        {
            return source.StartActivity(source.Name)!;
        }

        public Activity Start(string name)
        {
            return source.StartActivity(name)!;
        }
    }
}