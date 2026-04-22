using Infrastructure;

namespace Cluster.Deploy;

public static class DeployIdPipe
{
    public static readonly IRuntimePipeId Id = new RuntimePipeId("deploy-id");
}

[GenerateSerializer]
public class DeployIdRequest
{
}

[GenerateSerializer]
public class DeployIdResponse
{
    [Id(0)] public Guid DeployId { get; set; }
}