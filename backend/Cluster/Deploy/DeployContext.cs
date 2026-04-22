using Common.Reactive;
using Microsoft.Extensions.Logging;

namespace Cluster.Deploy;

public interface IDeployContext
{
    Guid DeployId { get; }

    IReadOnlyLifetime DeployLifetime { get; }

    Task Set(Guid deployId, IReadOnlyLifetime parentLifetime);
}

public class DeployContext : IDeployContext
{
    public DeployContext(
        IEnumerable<IDeployAware> subscribers,
        ILogger<DeployContext> logger)
    {
        _subscribers = subscribers.ToArray();
        _logger = logger;
    }

    private readonly IReadOnlyList<IDeployAware> _subscribers;
    private readonly ILogger<DeployContext> _logger;

    private Lifetime? _deployLifetime;
    private Guid _deployId;

    public Guid DeployId => _deployId;

    public IReadOnlyLifetime DeployLifetime =>
        _deployLifetime ?? throw new InvalidOperationException("Deploy not initialized");

    public async Task Set(Guid deployId, IReadOnlyLifetime parentLifetime)
    {
        if (_deployId == deployId && _deployLifetime is { IsTerminated: false })
            return;

        _deployLifetime?.Terminate();

        var newLifetime = new Lifetime();
        parentLifetime.Listen(() => newLifetime.Terminate());

        _deployLifetime = newLifetime;
        _deployId = deployId;

        _logger.LogInformation("[DeployContext] Deploy id set to {DeployId}", deployId);

        foreach (var subscriber in _subscribers)
        {
            try
            {
                await subscriber.OnDeployChanged(deployId, newLifetime);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[DeployContext] Subscriber {Subscriber} failed on deploy change",
                    subscriber.GetType().Name);
            }
        }
    }
}