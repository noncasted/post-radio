using Cluster.Discovery;
using Infrastructure;

namespace Cluster.Coordination;

public class MessagePipeServiceRequestId : IRuntimePipeId
{
    public MessagePipeServiceRequestId(IServiceOverview serviceOverview, Type type)
    {
        _serviceOverview = serviceOverview;
        _type = type;
    }

    private readonly IServiceOverview _serviceOverview;
    private readonly Type _type;

    public string ToRaw()
    {
        return $"service-pipe-request-{_serviceOverview.Id}-{_type.FullName}";
    }
}