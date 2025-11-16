using Common;
using Infrastructure.Loop;
using Infrastructure.Messaging;
using Infrastructure.Orleans;
using Infrastructure.StorableActions;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Coordination;

public interface IClusterFeatures
{
    IViewableProperty<bool> AcceptingConnections { get; }

    Task SetAcceptingConnections(bool accepting);
}

public class ClusterFeatures : ClusterState<ClusterFeaturesState>, IClusterFeatures
{
    public ClusterFeatures(IOrleans orleans, IMessaging messaging) : base(orleans, messaging)
    {
    }

    private readonly ViewableProperty<bool> _acceptingConnections = new(false);

    public IViewableProperty<bool> AcceptingConnections => _acceptingConnections;

    public Task SetAcceptingConnections(bool accepting)
    {
        Value.AcceptingConnections = accepting;
        return SetValue(Value);
    }

    protected override void OnSetup(IReadOnlyLifetime lifetime)
    {
        this.View(lifetime, value => { _acceptingConnections.Set(value.AcceptingConnections); });
    }
}

[GenerateSerializer]
public class ClusterFeaturesState
{
    [Id(0)]
    public bool AcceptingConnections { get; set; }
}

public static class ClusterFeaturesExtensions
{
    public static IHostApplicationBuilder AddClusterFeatures(this IHostApplicationBuilder builder)
    {
        builder.Services.Add<ClusterFeatures>()
            .As<IClusterState<ClusterFeaturesState>>()
            .As<IClusterFeatures>()
            .As<ILocalSetupCompleted>();

        return builder;
    }
}