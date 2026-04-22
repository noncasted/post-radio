using Cluster.Discovery;
using Cluster.State;
using Common.Extensions;
using Common.Reactive;
using Infrastructure.State;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cluster.Deploy;

public interface IDeployCleanup
{
    Task<DeployCleanupResult> Run();
}

public class DeployCleanupResult
{
    public required int Removed { get; init; }
    public required IReadOnlyList<Guid> RemovedDeployIds { get; init; }
    public required Guid KeptDeployId { get; init; }
}

public class DeployCleanup : IDeployCleanup
{
    public DeployCleanup(
        IDeployContext deployContext,
        IStateStorage stateStorage,
        ILogger<DeployCleanup> logger)
    {
        _deployContext = deployContext;
        _stateStorage = stateStorage;
        _logger = logger;
    }

    private readonly IDeployContext _deployContext;
    private readonly IStateStorage _stateStorage;
    private readonly ILogger<DeployCleanup> _logger;

    public async Task<DeployCleanupResult> Run()
    {
        var currentDeployId = _deployContext.DeployId;

        if (currentDeployId == Guid.Empty)
            throw new InvalidOperationException("No active deploy, refusing to run cleanup");

        var lifetime = new Lifetime();

        try
        {
            var toDelete = new List<StateIdentity>();
            var staleIds = new HashSet<Guid>();

            await Collect<DeployState>(lifetime, currentDeployId, toDelete, staleIds);
            await Collect<ServiceDiscoveryStorageState>(lifetime, currentDeployId, toDelete, staleIds);
            await Collect<ClusterFeaturesState>(lifetime, currentDeployId, toDelete, staleIds);

            if (toDelete.Count > 0)
            {
                await _stateStorage.Delete(new StateDeleteRequest { Identities = toDelete });

                _logger.LogInformation(
                    "[DeployCleanup] Removed {Count} stale deploy records across {DeployCount} deploys, kept {Current}",
                    toDelete.Count, staleIds.Count, currentDeployId);
            }
            else
            {
                _logger.LogInformation("[DeployCleanup] No stale deploy records to remove");
            }

            return new DeployCleanupResult
            {
                Removed = toDelete.Count,
                RemovedDeployIds = staleIds.ToList(),
                KeptDeployId = currentDeployId
            };
        }
        finally
        {
            lifetime.Terminate();
        }
    }

    private async Task Collect<T>(
        IReadOnlyLifetime lifetime,
        Guid currentDeployId,
        List<StateIdentity> toDelete,
        HashSet<Guid> staleIds)
        where T : IStateValue, new()
    {
        var info = _stateStorage.Registry.Get<T>();

        await foreach (var (key, _) in _stateStorage.ReadAll<Guid, T>(lifetime))
        {
            if (key == currentDeployId)
                continue;

            toDelete.Add(new StateIdentity
            {
                Key = key,
                Type = info.Name,
                TableName = info.TableName,
                Extension = null
            });

            staleIds.Add(key);
        }
    }
}

public static class DeployCleanupExtensions
{
    public static IHostApplicationBuilder AddDeployCleanup(this IHostApplicationBuilder builder)
    {
        builder.Add<DeployCleanup>()
               .As<IDeployCleanup>();

        return builder;
    }
}