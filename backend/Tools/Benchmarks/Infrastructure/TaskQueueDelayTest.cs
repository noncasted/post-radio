using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace Benchmarks;

public class TaskQueueDelayTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)]
        public int Iterations { get; set; } = 500000;

        [Id(1)]
        public int Concurrent { get; set; } = 1;
    }

    public class Root : BenchmarkRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils) : base(utils)
        {
        }

        public override string Group => TestGroups.Infrastructure;
        public override string Title => "task-queue-delay";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            await handle.RunConcurrentIterations(payload, Process);

            return;

            async Task Process()
            {
                var queue = new TaskQueue(NullLogger<TaskQueue>.Instance);
                queue.Enqueue(new TestPriorityTask($"immediate-{Guid.NewGuid()}", TaskPriority.Medium));

                queue.Enqueue(new TestPriorityTask($"delayed-{Guid.NewGuid()}", TaskPriority.Medium,
                    TimeSpan.FromSeconds(60)));
                queue.Collect();
                handle.Metrics.Inc();
                await Task.CompletedTask;
            }
        }
    }
}