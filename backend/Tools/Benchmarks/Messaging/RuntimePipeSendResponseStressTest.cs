using System.Diagnostics.CodeAnalysis;
using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

public class MessagePipeSendResponseStressTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload()
    {
        [Id(0)]
        public required int MessageCount { get; init; } = 27000;
    }

    [GenerateSerializer]
    public class RequestPayload
    {
        [Id(0)]
        public required string Service { get; init; }

        [Id(1)]
        public required int MessageIndex { get; init; }
    }

    [GenerateSerializer]
    public class ResponsePayload
    {
        [Id(0)]
        public required string Message { get; init; }

        [Id(1)]
        public required long Timestamp { get; init; }

        [Id(2)]
        public required string ProcessedBy { get; init; }
    }

    public static string TestName => "messaging-pipe-send-response-stress-test";

    public class Root : BenchmarkRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils) : base(utils)
        {
        }

        public override string Group => TestGroups.Messaging;
        public override string Subgroup => TestGroups.Subgroups.RuntimePipe;
        public override string Title => "Request-response throughput";
        public override string MetricName => "msg/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            var completion = new TaskCompletionSource();
            var totalMessages = payload.MessageCount * 5;
            var processedCount = 0;

            handle.Progress.Log("Setting up request-response handler...");

            await Messaging.AddPipeRequestHandler<RequestPayload, ResponsePayload>(handle.Lifetime,
                new RuntimePipeId(TestName),
                OnRequest);

            handle.Progress.SetStatus(OperationStatus.InProgress);
            handle.Progress.Log("Starting test nodes...");

            await Task.WhenAll(handle.StartNode(ServiceTag.Meta, TestName, payload),
                handle.StartNode(ServiceTag.Coordinator, TestName, payload),
                handle.StartNode(ServiceTag.Silo, TestName, payload),
                handle.StartNode(ServiceTag.Console, TestName, payload));

            await completion.Task;

            return;

            async Task<ResponsePayload> OnRequest(RequestPayload request)
            {
                var count = Interlocked.Increment(ref processedCount);

                handle.Metrics.Inc();
                handle.Progress.SetProgress((float)count / totalMessages);
                handle.Progress.Log($"Processed {count}/{totalMessages} requests");

                var response = new ResponsePayload
                {
                    Message = $"Processed request {request.MessageIndex} from {request.Service}",
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ProcessedBy = Environment.Tag.ToString()
                };

                if (count >= totalMessages)
                {
                    await Task.Delay(100);
                    completion.SetResult();
                }

                return response;
            }
        }
    }

    public class Node : BenchmarkNode<StartPayload>
    {
        public Node(IOrleans orleans, ClusterTestUtils utils) : base(utils)
        {
            _orleans = orleans;
        }

        private readonly IOrleans _orleans;

        protected override string Name => TestName;

        protected override async Task Run(IReadOnlyLifetime lifetime, StartPayload payload)
        {
            for (var i = 0; i < payload.MessageCount; i++)
            {
                try
                {
                    await Messaging.SendPipe<ResponsePayload>(new RuntimePipeId(TestName),
                        new RequestPayload
                        {
                            Service = Environment.Tag.ToString(),
                            MessageIndex = i + 1
                        });
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Failed to send request {Index}/{Total}", i + 1, payload.MessageCount);
                }
            }
        }
    }
}