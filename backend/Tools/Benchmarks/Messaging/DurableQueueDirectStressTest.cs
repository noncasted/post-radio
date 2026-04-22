using System.Diagnostics.CodeAnalysis;
using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

public class MessagingDirectQueueStressTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload()
    {
        [Id(0)]
        public required int MessageCount { get; init; } = 2000;
    }

    [GenerateSerializer]
    public class MessagePayload
    {
    }

    public static string TestName => "messaging-queue-direct-stress-test";

    public class Root : BenchmarkRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils) : base(utils)
        {
        }

        public override string Group => TestGroups.Messaging;
        public override string Subgroup => TestGroups.Subgroups.DurableQueue;
        public override string Title => "Direct push throughput";
        public override string MetricName => "msg/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            var completion = new TaskCompletionSource();
            var totalMessages = payload.MessageCount * 5;
            var receivedCount = 0;

            handle.Progress.Log("Listening for messages...");

            await Messaging.ListenDurableQueue<MessagePayload>(handle.Lifetime, new DurableQueueId(TestName),
                OnMessage);

            handle.Progress.SetStatus(OperationStatus.InProgress);
            handle.Progress.Log("Starting test node...");

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
                    completion.SetResult();
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
                    await Messaging.PushDirectQueue(new DurableQueueId(TestName), new MessagePayload());
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Failed to send message {MessageIndex}/{TotalMessages}",
                        i + 1,
                        payload.MessageCount);
                }
            }
        }
    }
}