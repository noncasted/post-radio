using Cluster.Deploy;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Orchestration;

public class CoordinatorReadyHealthCheck : IHealthCheck
{
    public CoordinatorReadyHealthCheck(IDeployContext deployContext)
    {
        _deployContext = deployContext;
    }

    private readonly IDeployContext _deployContext;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var deployId = _deployContext.DeployId;

        var result = deployId != Guid.Empty
            ? HealthCheckResult.Healthy($"Deploy identity assigned: {deployId}")
            : HealthCheckResult.Unhealthy("Deploy identity not assigned yet");

        return Task.FromResult(result);
    }
}