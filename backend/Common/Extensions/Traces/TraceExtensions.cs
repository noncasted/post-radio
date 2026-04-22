using System.Diagnostics;

namespace Common.Extensions;

public static class TraceExtensions
{
    public static readonly ActivitySource PlayerEndpoints = new("Player.Endpoints");
    public static readonly ActivitySource PlayerConnection = new("Player.Connection");
    public static readonly ActivitySource Transactions = new("Infrastructure.Transactions");
    public static readonly ActivitySource SideEffects = new("Infrastructure.SideEffects");
    public static readonly ActivitySource MessagingDurableQueue = new("Infrastructure.Messaging.DurableQueue");
    public static readonly ActivitySource MessagingRuntimePipe = new("Infrastructure.Messaging.RuntimePipe");
    public static readonly ActivitySource MessagingRuntimeChannel = new("Infrastructure.Messaging.RuntimeChannel");
    public static readonly ActivitySource TaskBalancer = new("Infrastructure.TaskBalancer");

    public static readonly IEnumerable<ActivitySource> AllSources =
    [
        PlayerEndpoints,
        PlayerConnection,
        Transactions,
        SideEffects,
        MessagingDurableQueue,
        MessagingRuntimePipe,
        MessagingRuntimeChannel,
        TaskBalancer
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