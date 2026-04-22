namespace Benchmarks;

public class TestGroups
{
    public const string Messaging = "Messaging";
    public const string State = "State";
    public const string Meta = "Meta";
    public const string Infrastructure = "Infrastructure";

    public static class Subgroups
    {
        public const string RuntimeChannel = "RuntimeChannel";
        public const string RuntimePipe = "RuntimePipe";
        public const string DurableQueue = "DurableQueue";
    }
}