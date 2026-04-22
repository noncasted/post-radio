using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace Benchmarks;

public class TaskQueueCollectTest
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
        public override string Title => "task-queue-collect";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var queue = new TaskQueue(NullLogger<TaskQueue>.Instance);

            await handle.RunConcurrentIterations(payload, Process);

            return;

            async Task Process()
            {
                queue.Enqueue(new TestPriorityTask($"a-{Guid.NewGuid()}", TaskPriority.Medium));
                queue.Enqueue(new TestPriorityTask($"b-{Guid.NewGuid()}", TaskPriority.High));
                queue.Enqueue(new TestPriorityTask($"c-{Guid.NewGuid()}", TaskPriority.Low));
                queue.Collect();
                handle.Metrics.Inc();
                await Task.CompletedTask;
            }
        }
    }
}