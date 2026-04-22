namespace Infrastructure;

public interface ICorrelatedSideEffect
{
    Guid CorrelationId { get; }
}

[GenerateSerializer]
public class DurableQueueSideEffect : ISideEffect, ICorrelatedSideEffect
{
    [Id(0)]
    public required string QueueName { get; set; }

    [Id(1)]
    public required object Message { get; set; }

    [Id(2)]
    public Guid CorrelationId { get; set; }

    public Task Execute(IOrleans orleans)
    {
        var queue = orleans.GetGrain<IDurableQueue>(QueueName);
        return queue.Push(Message);
    }
}