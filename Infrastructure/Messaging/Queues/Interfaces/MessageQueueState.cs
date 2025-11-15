using Infrastructure.StorableActions;

namespace Infrastructure.Messaging;

[GenerateSerializer]
public class MessageQueueState : BatchWriterState<object>
{
    
}