using Cluster.Discovery;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

public class ClusterTestUtils
{
    public ClusterTestUtils(
        IMessaging messaging,
        IServiceEnvironment environment,
        IStateStorage stateStorage,
        BenchmarkStorage benchmarkStorage,
        ILogger<ClusterTestUtils> logger,
        ILogger<TestCleanup> cleanupLogger)
    {
        Messaging = messaging;
        Environment = environment;
        StateStorage = stateStorage;
        BenchmarkStorage = benchmarkStorage;
        Logger = logger;
        Cleanup = new TestCleanup(stateStorage, cleanupLogger);
    }

    public readonly IMessaging Messaging;
    public readonly IServiceEnvironment Environment;
    public readonly IStateStorage StateStorage;
    public readonly BenchmarkStorage BenchmarkStorage;
    public readonly ILogger<ClusterTestUtils> Logger;
    public readonly TestCleanup Cleanup;

    public Task StartNode(ServiceTag service, string nodeName, object? payload = null)
    {
        var pipeId = new ClusterTestNodeMessages.NodePipeId(service, nodeName);

        var request = new ClusterTestNodeMessages.StartRequest
        {
            Service = service,
            NodeName = nodeName,
            Payload = payload
        };

        return Messaging.SendPipe<ClusterTestNodeMessages.StartResponse>(pipeId, request);
    }

    public Task TerminateNode(ServiceTag service, string nodeName)
    {
        var channelId = new ClusterTestNodeMessages.NodeChannelId(service, nodeName);
        return Messaging.PublishChannel(channelId, new ClusterTestNodeMessages.Terminate());
    }
}