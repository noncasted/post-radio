using Common.Extensions;
using Common.Reactive;
using Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

[GenerateSerializer]
public class AddressableStateValue : IStateValue
{
    [Id(0)]
    public string Value { get; set; } = string.Empty;

    [Id(1)]
    public bool IsInitialized { get; set; }

    [Id(2)]
    public DateTime UpdateDate { get; set; }

    public int Version => 0;
}

public interface IAddressableState<T> : IViewableProperty<T> where T : class, new()
{
    bool IsInitialized { get; }

    Task SetValue(T value);
}

public class AddressableStateChannelId<T> : IRuntimeChannelId
{
    public required string Name { get; init; }

    public string ToRaw()
    {
        return $"addressable-state-{Name}";
    }
}

public class AddressableStateUtils
{
    public AddressableStateUtils(IOrleans orleans, IMessaging messaging, ILoggerFactory loggerFactory)
    {
        Orleans = orleans;
        Messaging = messaging;
        LoggerFactory = loggerFactory;
    }

    public IOrleans Orleans { get; }
    public IMessaging Messaging { get; }
    public ILoggerFactory LoggerFactory { get; }
}

public class AddressableState<T> : ViewableProperty<T>, IOrleansStarted, IAddressableState<T>
    where T : class, new()
{
    public AddressableState(AddressableStateUtils utils) : base(new T())
    {
        _orleans = utils.Orleans;
        _messaging = utils.Messaging;
        _logger = utils.LoggerFactory.CreateLogger<AddressableState<T>>();

        var stateInfo = _orleans.StateStorage.Registry.Get<T>();

        _identity = new StateIdentity
        {
            Key = stateInfo.Name,
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        };

        _channelId = new AddressableStateChannelId<T>
        {
            Name = _identity.Type
        };
    }

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;
    private readonly ILogger<AddressableState<T>> _logger;
    private readonly StateIdentity _identity;
    private readonly AddressableStateChannelId<T> _channelId;

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _isInitialized;
    private DateTime _updateDate;

    public bool IsInitialized => _isInitialized;
    public DateTime UpdateDate => _updateDate;

    public async Task OnOrleansStarted(IReadOnlyLifetime lifetime)
    {
        try
        {
            var state = await _orleans.StateStorage.Read<AddressableStateValue>(_identity);

            if (state.IsInitialized == false)
            {
                _logger.LogDebug("[AddressableState] {Type} not yet initialized, skipping load", typeof(T).Name);
            }
            else
            {
                var value = _orleans.Serializer.Deserialize<T>(state.Value);
                Set(value);
            }

            await _messaging.ListenChannel<AddressableStateValue>(lifetime, _channelId, OnUpdate);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[AddressableState] Failed to initialize {Type}", typeof(T).Name);
        }
    }

    public async Task SetValue(T value)
    {
        await _writeLock.WaitAsync();

        try
        {
            _isInitialized = true;
            _updateDate = DateTime.UtcNow;

            Set(value);

            var state = new AddressableStateValue()
            {
                IsInitialized = true,
                UpdateDate = _updateDate,
                Value = _orleans.Serializer.Serialize(value)
            };

            await _orleans.StateStorage.Write(_identity, state);
            await _messaging.PublishChannel(_channelId, state);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[AddressableState] Failed to persist {Type}", typeof(T).Name);
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void OnUpdate(AddressableStateValue state)
    {
        _isInitialized = true;
        _updateDate = DateTime.UtcNow;

        try
        {
            var value = _orleans.Serializer.Deserialize<T>(state.Value);
            Set(value);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[AddressableState] Failed to deserialize update for {Type}", typeof(T).Name);
        }
    }
}

public static class AddressableStateExtensions
{
    public static ContainerExtensions.Registration AddAddressableState<T>(this IHostApplicationBuilder builder)
        where T : class, IOrleansStarted
    {
        builder.Services.AddSingleton<AddressableStateUtils>();

        return builder.Add<T>()
                      .As<IOrleansStarted>();
    }
}