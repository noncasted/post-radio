namespace Infrastructure.StorableActions;

[GenerateSerializer]
public class BatchWriterState<T>
{
    [Id(0)] public readonly List<T> Entries = new();
}