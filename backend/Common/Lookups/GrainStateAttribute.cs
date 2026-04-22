namespace Common
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class GrainStateAttribute : Attribute
    {
        public required string Table { get; init; }
        public required string State { get; init; }
        public required string Lookup { get; init; }
        public required GrainKeyType Key { get; init; }
    }
}