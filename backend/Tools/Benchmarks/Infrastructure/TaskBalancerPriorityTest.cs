using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace Benchmarks;

public class TaskBalancerPriorityTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)]
        public int Iterations { get; set; } = 5000;

        [Id(1)]
        public int Concurrent { get; set; } = 4;
    }

    public class Root : BenchmarkRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils) : base(utils)
        {
        }

        public override string Group => TestGroups.Infrastructure;
        public override string Title => "task-balancer-priority";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var queue = new TaskQueue(NullLogger<TaskQueue>.Instance);

            var config = new TestBalancerConfig(new TaskBalancerOptions
            {
                EmptyDelayMs = 1,
                NextDelayMs = 0,
                ConcurrentTasks = 4,
                IterationScore = 0
            });

            var balancer = new TaskBalancer(queue, NullLogger<TaskBalancer>.Instance, config);
            _ = balancer.Run(handle.Lifetime);

            var priorities = new[] { TaskPriority.Critical, TaskPriority.High, TaskPriority.Medium, TaskPriority.Low };
            var priorityIndex = 0;

            await handle.RunConcurrentIterations(payload, Process);

            return;

            async Task Process()
            {
                var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var priority = priorities[Interlocked.Increment(ref priorityIndex) % priorities.Length];

                queue.Enqueue(new TestPriorityTask(Guid.NewGuid().ToString(),
                    priority,
                    execute: () => {
                        tcs.SetResult();
                        return Task.CompletedTask;
                    }));

                await tcs.Task;
                handle.Metrics.Inc();
            }
        }
    }
}