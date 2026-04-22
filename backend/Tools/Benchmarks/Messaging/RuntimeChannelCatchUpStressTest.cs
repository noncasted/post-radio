using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;

namespace Benchmarks;

public class RuntimeChannelCatchUpStressTest
{
    [GenerateSerializer]
    public class TestPayload
    {
        [Id(0)] public int Index { get; set; }
    }

    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload()
    {
        [Id(0)]
        public int MessageCount { get; set; } = 5000;

        [Id(1)]
        public int DisconnectCount { get; set; } = 10;
    }

    public static string TestName => "runtime-channel-catchup-stress";

    public class Root : BenchmarkRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils) : base(utils)
        {
        }

        public override string Group => TestGroups.Messaging;
        public override string Subgroup => TestGroups.Subgroups.RuntimeChannel;
        public override string Title => "Catch-up stress (disconnect/reconnect)";
        public override string MetricName => "msg/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var channelId = new RuntimeChannelId(TestName);
            var totalMessages = payload.MessageCount;
            var disconnectInterval = totalMessages / payload.DisconnectCount;
            var totalReceived = 0;

            handle.Progress.Log("Starting publish with periodic disconnect/reconnect...");

            var lifetime = new Lifetime();
            var batchDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var batchTarget = disconnectInterval;

            await Messaging.ListenChannel<TestPayload>(lifetime, channelId, _ => {
                var count = Interlocked.Increment(ref totalReceived);
                handle.Metrics.Inc();

                if (count >= batchTarget)
                    batchDone.TrySetResult();
            });

            for (var i = 0; i < totalMessages; i++)
            {
                await Messaging.PublishChannel(channelId, new TestPayload { Index = i });

                if (i > 0 && i % disconnectInterval == 0)
                {
                    // Wait for batch delivery
                    await batchDone.Task.WaitAsync(TimeSpan.FromSeconds(10));

                    // Disconnect
                    lifetime.Terminate();

                    // Publish a few messages while disconnected
                    for (var j = 0; j < 10; j++)
                        await Messaging.PublishChannel(channelId, new TestPayload { Index = totalMessages + j });

                    // Reconnect — catch-up should replay missed messages
                    lifetime = new Lifetime();
                    batchTarget = Volatile.Read(ref totalReceived) + disconnectInterval;
                    batchDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                    await Messaging.ListenChannel<TestPayload>(lifetime, channelId, _ => {
                        var count = Interlocked.Increment(ref totalReceived);
                        handle.Metrics.Inc();

                        if (count >= batchTarget)
                            batchDone.TrySetResult();
                    });

                    handle.Progress.SetProgress((float)i / totalMessages);
                    handle.Progress.Log($"Reconnected at {i}, total received: {totalReceived}");
                }
            }

            // Wait for final delivery
            await Task.Delay(TimeSpan.FromSeconds(2));
            lifetime.Terminate();

            handle.Progress.Log($"Done. Total received: {totalReceived} (published: {totalMessages})");
            handle.Progress.SetProgress(1f);
        }
    }
}