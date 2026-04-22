using Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Orchestration;

public class OrleansReadyHealthCheck : IHealthCheck
{
    public OrleansReadyHealthCheck(IServiceLoopObserver loopObserver)
    {
        _loopObserver = loopObserver;
    }

    private readonly IServiceLoopObserver _loopObserver;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var started = _loopObserver.IsOrleansStarted.Value;

        var result = started == true
            ? HealthCheckResult.Healthy("Orleans runtime started")
            : HealthCheckResult.Unhealthy("Orleans runtime not started yet");

        return Task.FromResult(result);
    }
}