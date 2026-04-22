using Cluster.Discovery;
using Infrastructure;

namespace Benchmarks;

public static class ClusterTestNodeMessages
{
    [GenerateSerializer]
    public class StartRequest
    {
        [Id(0)] public required ServiceTag Service { get; init; }
        [Id(1)] public required string NodeName { get; init; }
        [Id(2)] public object? Payload { get; set; }
    }

    [GenerateSerializer]
    public class StartResponse
    {
    }

    [GenerateSerializer]
    public class Terminate
    {
    }

    public class NodePipeId : IRuntimePipeId
    {
        public NodePipeId(ServiceTag service, string name)
        {
            _service = service;
            _name = name;
        }

        private readonly ServiceTag _service;
        private readonly string _name;

        public string ToRaw()
        {
            return $"cluster-test-node-{_service.ToString()}-{_name}-start";
        }
    }

    public class NodeChannelId : IRuntimeChannelId
    {
        public NodeChannelId(ServiceTag service, string name)
        {
            _service = service;
            _name = name;
        }

        private readonly ServiceTag _service;
        private readonly string _name;

        public string ToRaw()
        {
            return $"cluster-test-node-{_service.ToString()}-{_name}-terminate";
        }
    }
}