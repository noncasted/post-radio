using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Cluster.Discovery;

public class ServiceDiscoveryStorage : Grain, IServiceDiscoveryStorage
{
    public ServiceDiscoveryStorage(
        [State] State<ServiceDiscoveryStorageState> state,
        ILogger<ServiceDiscoveryStorage> logger)
    {
        _state = state;
        _logger = logger;
    }

    private readonly State<ServiceDiscoveryStorageState> _state;
    private readonly ILogger<ServiceDiscoveryStorage> _logger;

    private static readonly TimeSpan StaleThreshold = TimeSpan.FromSeconds(30);

    public async Task<Dictionary<Guid, IServiceOverview>> Update(IServiceOverview overview)
    {
        var deployId = this.GetPrimaryKey();
        var now = DateTime.UtcNow;
        var staleCutoff = now - StaleThreshold;

        var value = await _state.Update(s => {
            s.Members[overview.Id] = overview;

            var staleIds = s.Members.Values
                            .Where(m => m.UpdateTime < staleCutoff)
                            .Select(m => m.Id)
                            .ToList();

            foreach (var id in staleIds)
            {
                var member = s.Members[id];
                s.Members.Remove(id);

                _logger.LogInformation(
                    "[ServiceDiscoveryStorage] Removed stale member {MemberId} (tag={Tag}, lastUpdate={LastUpdate}) from deploy {DeployId}",
                    id, member.Tag, member.UpdateTime, deployId);
            }
        });

        return value.Members;
    }

    public async Task<Dictionary<Guid, IServiceOverview>> Get()
    {
        var value = await _state.ReadValue();
        return value.Members;
    }

    public async Task Unregister(Guid serviceId)
    {
        await _state.Write(s => s.Members.Remove(serviceId));

        _logger.LogInformation("[ServiceDiscoveryStorage] Unregistered member {MemberId} from deploy {DeployId}",
            serviceId, this.GetPrimaryKey());
    }
}