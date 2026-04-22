using Cluster.Deploy;
using Common;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cluster.State;

public interface IClusterFeatures : IViewableProperty<ClusterFeaturesState>, IClusterFlags
{
    bool IsInitialized { get; }

    Task SetAcceptingConnections(bool accepting);
    Task SetMatchmakingEnabled(bool enabled);
    Task SetSideEffectsEnabled(bool enabled);
    Task SetSnapshotDiffGuardEnabled(bool enabled);
}

public class ClusterFeaturesChannelId : IRuntimeChannelId
{
    public ClusterFeaturesChannelId(Guid deployId)
    {
        _deployId = deployId;
    }

    private readonly Guid _deployId;

    public string ToRaw() => $"cluster-features-{_deployId:N}";
}

public class ClusterFeatures : ViewableProperty<ClusterFeaturesState>, IClusterFeatures, IDeployAware
{
    public ClusterFeatures(
        IOrleans orleans,
        IMessaging messaging,
        ILogger<ClusterFeatures> logger)
        : base(new ClusterFeaturesState())
    {
        _orleans = orleans;
        _messaging = messaging;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly IMessaging _messaging;
    private readonly ILogger<ClusterFeatures> _logger;

    private Guid _deployId;
    private volatile bool _isInitialized;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public bool IsInitialized => _isInitialized;

    bool IClusterFlags.MatchmakingEnabled => Value.MatchmakingEnabled;
    bool IClusterFlags.SideEffectsEnabled => Value.SideEffectsEnabled;
    bool IClusterFlags.SnapshotDiffGuardEnabled => Value.SnapshotDiffGuardEnabled;

    public async Task OnDeployChanged(Guid newDeployId, IReadOnlyLifetime deployLifetime)
    {
        _deployId = newDeployId;

        try
        {
            var grain = _orleans.GetGrain<IClusterFeaturesGrain>(newDeployId);
            var state = await grain.Get();
            Set(state);
            _isInitialized = true;

            var channelId = new ClusterFeaturesChannelId(newDeployId);
            await _messaging.ListenChannel<ClusterFeaturesState>(deployLifetime, channelId, OnRemoteUpdate);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[ClusterFeatures] Failed to initialize for deploy {DeployId}", newDeployId);
        }
    }

    public Task SetAcceptingConnections(bool accepting) => ApplyUpdate(s => s.AcceptingConnections = accepting);

    public Task SetMatchmakingEnabled(bool enabled) => ApplyUpdate(s => s.MatchmakingEnabled = enabled);

    public Task SetSideEffectsEnabled(bool enabled) => ApplyUpdate(s => s.SideEffectsEnabled = enabled);

    public Task SetSnapshotDiffGuardEnabled(bool enabled) => ApplyUpdate(s => s.SnapshotDiffGuardEnabled = enabled);

    private async Task ApplyUpdate(Action<ClusterFeaturesState> mutator)
    {
        await _writeLock.WaitAsync();

        try
        {
            if (_deployId == Guid.Empty)
                throw new InvalidOperationException("Cluster features not attached to a deploy yet");

            var grain = _orleans.GetGrain<IClusterFeaturesGrain>(_deployId);

            var next = new ClusterFeaturesState
            {
                AcceptingConnections = Value.AcceptingConnections,
                MatchmakingEnabled = Value.MatchmakingEnabled,
                SideEffectsEnabled = Value.SideEffectsEnabled,
                SnapshotDiffGuardEnabled = Value.SnapshotDiffGuardEnabled
            };

            mutator(next);

            var stored = await grain.Set(next);
            Set(stored);

            await _messaging.PublishChannel(new ClusterFeaturesChannelId(_deployId), stored);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[ClusterFeatures] Failed to apply update for deploy {DeployId}", _deployId);
            throw;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void OnRemoteUpdate(ClusterFeaturesState state)
    {
        Set(state);
    }
}

[GenerateSerializer]
[GrainState(Table = "cluster", State = "cluster_features", Lookup = "ClusterFeatures", Key = GrainKeyType.Guid)]
public class ClusterFeaturesState : IStateValue
{
    [Id(0)] public bool AcceptingConnections { get; set; } = false;
    [Id(1)] public bool MatchmakingEnabled { get; set; } = false;
    [Id(2)] public bool SideEffectsEnabled { get; set; } = false;
    [Id(3)] public bool SnapshotDiffGuardEnabled { get; set; } = false;

    public int Version => 0;
}

public interface IClusterFeaturesGrain : IGrainWithGuidKey
{
    Task<ClusterFeaturesState> Get();

    Task<ClusterFeaturesState> Set(ClusterFeaturesState state);
}

public class ClusterFeaturesGrain : Grain, IClusterFeaturesGrain
{
    public ClusterFeaturesGrain([State] State<ClusterFeaturesState> state)
    {
        _state = state;
    }

    private readonly State<ClusterFeaturesState> _state;

    public Task<ClusterFeaturesState> Get()
    {
        return _state.ReadValue();
    }

    public async Task<ClusterFeaturesState> Set(ClusterFeaturesState state)
    {
        return await _state.Update(s => {
            s.AcceptingConnections = state.AcceptingConnections;
            s.MatchmakingEnabled = state.MatchmakingEnabled;
            s.SideEffectsEnabled = state.SideEffectsEnabled;
            s.SnapshotDiffGuardEnabled = state.SnapshotDiffGuardEnabled;
        });
    }
}

public static class ClusterFeaturesExtensions
{
    public static IHostApplicationBuilder AddClusterFeatures(this IHostApplicationBuilder builder)
    {
        builder.Add<ClusterFeatures>()
               .As<IClusterFeatures>()
               .As<IDeployAware>();

        builder.Services.AddSingleton<IClusterFlags>(sp => sp.GetRequiredService<IClusterFeatures>());

        return builder;
    }
}