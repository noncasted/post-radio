using Common;
using Infrastructure.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Discovery;

public interface IServiceDiscovery
{
    IServiceOverview Self { get; }
    IReadOnlyDictionary<Guid, IServiceOverview> Entries { get; }
    
    Task Start(IReadOnlyLifetime lifetime);
}

public class ServiceDiscovery : IServiceDiscovery
{
    public ServiceDiscovery(
        IMessaging messaging,
        IServiceEnvironment environment,
        ILogger<ServiceDiscovery> logger)
    {
        _messaging = messaging;
        _environment = environment;
        _logger = logger;

        _self = CreateOverview();
    }

    private readonly IMessaging _messaging;
    private readonly IServiceEnvironment _environment;
    private readonly ILogger<ServiceDiscovery> _logger;
    private readonly Dictionary<Guid, IServiceOverview> _entries = new();
    private readonly IMessageQueueId _queueId = new MessageQueueId("service-discovery");

    private IServiceOverview _self;

    public IServiceOverview Self => _self;
    public IReadOnlyDictionary<Guid, IServiceOverview> Entries => _entries;
    
    public Task Start(IReadOnlyLifetime lifetime)
    {
        _messaging.ListenQueue<IServiceOverview>(lifetime, _queueId, service => _entries[service.Id] = service);
        UpdateLoop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    private async Task UpdateLoop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            _self = CreateOverview();

            try
            {
                await _messaging.PushDirectQueue(_queueId, _self);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Discovery] Pushing service overview failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    private IServiceOverview CreateOverview()
    {
        return _environment.Tag switch
        {
            ServiceTag.Silo => new ServiceOverview()
            {
                Id = _environment.ServiceId,
                Tag = ServiceTag.Silo,
                UpdateTime = DateTime.UtcNow,
            },
            ServiceTag.Console => new ServiceOverview()
            {
                Id = _environment.ServiceId,
                Tag = ServiceTag.Console,
                UpdateTime = DateTime.UtcNow,
            },
            ServiceTag.Frontend => new ServiceOverview()
            {
                Id = _environment.ServiceId,
                Tag = ServiceTag.Frontend,
                UpdateTime = DateTime.UtcNow,
            },
            ServiceTag.Coordinator => new ServiceOverview()
            {
                Id = _environment.ServiceId,
                Tag = ServiceTag.Coordinator,
                UpdateTime = DateTime.UtcNow,
            },
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

public static class ServiceDiscoveryExtensions
{
    public static IHostApplicationBuilder AddServiceDiscovery(this IHostApplicationBuilder builder)
    {
        builder.Services.Add<ServiceDiscovery>()
            .As<IServiceDiscovery>();

        return builder;
    }
}