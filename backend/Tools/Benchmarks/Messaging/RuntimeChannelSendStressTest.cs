using System.Diagnostics.CodeAnalysis;
using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

public class RuntimeChannelSendStressTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload()
    {
        [Id(0)]
        public required int MessageCount { get; init; } = 75000;
    }

    [GenerateSerializer]
    public class MessagePayload
    {
        [Id(0)]
        public required string Service { get; init; }

        [Id(1)]
        public required int MessageIndex { get; init; }
    }

    public static string TestName => "runtime-channel-send-stress-test";

    public class Root : BenchmarkRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils) : base(utils)
        {
        }

        public override string Group => TestGroups.Messaging;
        public override string Subgroup => TestGroups.Subgroups.RuntimeChannel;
        public override string Title => "Distributed send throughput";
        public override string MetricName => "msg/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            var completion = new TaskCompletionSource();
            var totalMessages = payload.MessageCount * 5;
            var receivedCount = 0;

            handle.Progress.Log("Setting up channel listeners...");

            await Messaging.ListenChannel<MessagePayload>(handle.Lifetime,
                new RuntimeChannelId(TestName),
                OnMessage);

            handle.Progress.SetStatus(OperationStatus.InProgress);
            handle.Progress.Log("Starting test nodes...");

            await Task.WhenAll(handle.StartNode(ServiceTag.Meta, TestName, payload),
                handle.StartNode(ServiceTag.Coordinator, TestName, payload),
                handle.StartNode(ServiceTag.Silo, TestName, payload),
                handle.StartNode(ServiceTag.Console, TestName, payload));

            await completion.Task;

            return;

            void OnMessage(MessagePayload message)
            {
                var count = Interlocked.Increment(ref receivedCount);

                handle.Metrics.Inc();
                handle.Progress.SetProgress((float)count / totalMessages);
                handle.Progress.Log($"Received {count}/{totalMessages} messages");

                if (count >= totalMessages)
                    completion.TrySetResult();
            }
        }
    }

    public class Node : BenchmarkNode<StartPayload>
    {
        public Node(ClusterTestUtils utils) : base(utils)
        {
        }

        protected override string Name => TestName;

        protected override async Task Run(IReadOnlyLifetime lifetime, StartPayload payload)
        {
            for (var i = 0; i < payload.MessageCount; i++)
            {
                try
                {
                    await Messaging.PublishChannel(new RuntimeChannelId(TestName),
                        new MessagePayload
                        {
                            Service = Environment.Tag.ToString(),
                            MessageIndex = i + 1
                        });
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Failed to publish message {Index}/{Total}", i + 1, payload.MessageCount);
                }
            }
        }
    }
}