using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class StateCollectionUpdateStressTest
{
    [GenerateSerializer]
    public class TestStateValue
    {
        [Id(0)] public Guid Id { get; set; }
        [Id(1)] public string Value { get; set; } = string.Empty;
    }

    public class TestUpdateQueueId : IDurableQueueId
    {
        public string ToRaw() => "bench-state-collection-update";
    }

    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload()
    {
        [Id(0)]
        public int UpdateCount { get; set; } = 5000;
    }

    public class Root : BenchmarkRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils) : base(utils)
        {
        }

        public override string Group => TestGroups.Messaging;
        public override string Subgroup => TestGroups.Subgroups.DurableQueue;
        public override string Title => "StateCollection update throughput";
        public override string MetricName => "update/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var queueId = new TestUpdateQueueId();
            var totalUpdates = payload.UpdateCount;
            var receivedCount = 0;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await Messaging.ListenDurableQueue<StateCollectionUpdate<Guid, TestStateValue>>(handle.Lifetime, queueId,
                update => {
                    var count = Interlocked.Increment(ref receivedCount);
                    handle.Metrics.Inc();
                    handle.Progress.SetProgress((float)count / totalUpdates);

                    if (count >= totalUpdates)
                        completion.TrySetResult();
                });

            handle.Progress.Log($"Listener ready, sending {totalUpdates} updates...");

            for (var i = 0; i < totalUpdates; i++)
            {
                handle.CancellationToken.ThrowIfCancellationRequested();

                await Messaging.PushDirectQueue(queueId,
                    new StateCollectionUpdate<Guid, TestStateValue>
                    {
                        Key = Guid.NewGuid(),
                        Value = new TestStateValue { Id = Guid.NewGuid(), Value = $"val-{i}" },
                        UpdatedAt = DateTime.UtcNow
                    });
            }

            handle.Progress.Log($"Sent {totalUpdates} updates, waiting for delivery...");

            await completion.Task.WaitAsync(handle.CancellationToken);

            handle.Progress.Log($"All {totalUpdates} updates delivered");
            handle.Progress.SetProgress(1f);
        }
    }
}