using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;

namespace Benchmarks;

public class RuntimeChannelDeliveryTimeoutTest
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
        public int MessageCount { get; set; } = 10000;

        [Id(1)]
        public int SlowListenerCount { get; set; } = 3;
    }

    public static string TestName => "runtime-channel-delivery-timeout";

    public class Root : BenchmarkRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils) : base(utils)
        {
        }

        public override string Group => TestGroups.Messaging;
        public override string Subgroup => TestGroups.Subgroups.RuntimeChannel;
        public override string Title => "Delivery timeout (slow observers)";
        public override string MetricName => "msg/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var channelId = new RuntimeChannelId(TestName);
            var totalMessages = payload.MessageCount;
            var receivedCount = 0;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // Fast listener — tracks delivery
            await Messaging.ListenChannel<TestPayload>(handle.Lifetime, channelId, _ => {
                var count = Interlocked.Increment(ref receivedCount);
                handle.Metrics.Inc();
                handle.Progress.SetProgress((float)count / totalMessages);

                if (count >= totalMessages)
                    completion.TrySetResult();
            });

            // Slow listeners — will be timed out and removed
            for (var i = 0; i < payload.SlowListenerCount; i++)
            {
                var slowLifetime = new Lifetime();

                await Messaging.ListenChannel<TestPayload>(slowLifetime, channelId, async _ => {
                    // Simulate slow processing — exceeds DeliveryTimeoutSeconds (5s)
                    await Task.Delay(TimeSpan.FromSeconds(10));
                });
            }

            handle.Progress.Log($"Listeners ready: 1 fast + {payload.SlowListenerCount} slow. Publishing...");

            for (var i = 0; i < totalMessages; i++)
            {
                handle.CancellationToken.ThrowIfCancellationRequested();

                await Messaging.PublishChannel(channelId, new TestPayload { Index = i });
            }

            handle.Progress.Log($"Published {totalMessages}, waiting for fast listener to receive all...");

            await completion.Task.WaitAsync(handle.CancellationToken);

            handle.Progress.Log(
                $"Done. Fast listener received {receivedCount} messages. Slow listeners should have been removed by timeout.");
            handle.Progress.SetProgress(1f);
        }
    }
}