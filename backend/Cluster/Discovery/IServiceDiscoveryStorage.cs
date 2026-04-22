namespace Cluster.Discovery;

public interface IServiceDiscoveryStorage : IGrainWithGuidKey
{
    Task<Dictionary<Guid, IServiceOverview>> Update(IServiceOverview overview);

    Task<Dictionary<Guid, IServiceOverview>> Get();

    Task Unregister(Guid serviceId);
}