using System.Collections.Concurrent;
using Cluster.Deploy;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cluster.Discovery;

public interface IServiceDiscovery
{
    IServiceOverview Self { get; }
    IReadOnlyDictionary<Guid, IServiceOverview> Entries { get; }

    Task Start(IReadOnlyLifetime lifetime);
    Task Push();
}

public class ServiceDiscovery : IServiceDiscovery
{
    public ServiceDiscovery(
        IOrleans orleans,
        IDeployContext deployContext,
        IServiceEnvironment environment,
        ILogger<ServiceDiscovery> logger)
    {
        _orleans = orleans;
        _deployContext = deployContext;
        _environment = environment;
        _logger = logger;

        _self = CreateOverview();
    }

    private readonly IOrleans _orleans;
    private readonly IDeployContext _deployContext;
    private readonly IServiceEnvironment _environment;
    private readonly ILogger<ServiceDiscovery> _logger;
    private readonly ConcurrentDictionary<Guid, IServiceOverview> _entries = new();

    private volatile IServiceOverview _self;

    public IServiceOverview Self => _self;
    public IReadOnlyDictionary<Guid, IServiceOverview> Entries => _entries;

    public Task Start(IReadOnlyLifetime lifetime)
    {
        RefreshLoop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    public async Task Push()
    {
        try
        {
            _self = CreateOverview();

            if (_deployContext.DeployId == Guid.Empty)
                return;

            var grain = _orleans.GetGrain<IServiceDiscoveryStorage>(_deployContext.DeployId);
            var members = await grain.Update(_self);

            ApplyMembers(members);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[ServiceDiscovery] Push failed");
        }
    }

    private async Task RefreshLoop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            try
            {
                await Push();
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[ServiceDiscovery] RefreshLoop iteration failed");
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }

    private void ApplyMembers(Dictionary<Guid, IServiceOverview> members)
    {
        foreach (var (id, overview) in members)
            _entries[id] = overview;

        foreach (var existingId in _entries.Keys.ToList())
        {
            if (members.ContainsKey(existingId) == false)
                _entries.TryRemove(existingId, out _);
        }
    }

    private IServiceOverview CreateOverview()
    {
        return new ServiceOverview
        {
            Id = _environment.ServiceId,
            Tag = _environment.Tag,
            UpdateTime = DateTime.UtcNow,
        };
    }
}

public static class ServiceDiscoveryExtensions
{
    public static IHostApplicationBuilder AddServiceDiscovery(this IHostApplicationBuilder builder)
    {
        builder.Add<ServiceDiscovery>()
               .As<IServiceDiscovery>();

        return builder;
    }

}