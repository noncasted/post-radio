using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

public abstract class BenchmarkNode<TPayload> : ICoordinatorSetupCompleted
{
    public BenchmarkNode(ClusterTestUtils utils)
    {
        _utils = utils;
    }

    private readonly ClusterTestUtils _utils;

    private ILifetime _testLifetime = null!;

    public IMessaging Messaging => _utils.Messaging;
    public IServiceEnvironment Environment => _utils.Environment;
    public ILogger Logger => _utils.Logger;

    protected abstract string Name { get; }

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        await Messaging
            .AddPipeRequestHandler<ClusterTestNodeMessages.StartRequest, ClusterTestNodeMessages.StartResponse>(
                lifetime,
                new ClusterTestNodeMessages.NodePipeId(Environment.Tag, Name),
                OnStartRequest);

        await Messaging.ListenChannel<ClusterTestNodeMessages.Terminate>(lifetime,
            new ClusterTestNodeMessages.NodeChannelId(Environment.Tag, Name),
            OnTerminate);
    }

    private async Task<ClusterTestNodeMessages.StartResponse> OnStartRequest(
        ClusterTestNodeMessages.StartRequest request)
    {
        var payload = default(TPayload);

        if (request.Payload is not null)
            payload = (TPayload)request.Payload;

        _testLifetime = new Lifetime();
        Run(_testLifetime, payload!).NoAwait();
        return new ClusterTestNodeMessages.StartResponse();
    }

    private void OnTerminate(ClusterTestNodeMessages.Terminate message)
    {
        _testLifetime.Terminate();
    }

    protected abstract Task Run(IReadOnlyLifetime lifetime, TPayload payload);
}

public static class ClusterTestNodeExtensions
{
    public static IHostApplicationBuilder AddClusterTestNode<TNode>(this IHostApplicationBuilder builder)
        where TNode : class
    {
        builder.Add<TNode>()
               .As<ICoordinatorSetupCompleted>();

        return builder;
    }
}