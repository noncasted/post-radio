using Infrastructure.Messaging;

namespace Infrastructure.Coordination;

public class CoordinatorEvents
{
    public static readonly IMessageQueueId ReadyId = new ReadyPipeId();
    
    public class ReadyPipeId : IMessageQueueId
    {
        public string ToRaw() => "coordinator-ready";
    }

    [GenerateSerializer]
    public class ReadyPayload
    {
        
    }
}