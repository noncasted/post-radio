using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace Benchmarks;

public class TaskBalancerExceptionPenaltyTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)]
        public int Iterations { get; set; } = 1000;

        [Id(1)]
        public int Concurrent { get; set; } = 4;
    }

    public class Root : BenchmarkRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils) : base(utils)
        {
        }

        public override string Group => TestGroups.Infrastructure;
        public override string Title => "task-balancer-exception-penalty";
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
                IterationScore = 0,
                ExceptionPenalty = 1
            });

            var balancer = new TaskBalancer(queue, NullLogger<TaskBalancer>.Instance, config);
            _ = balancer.Run(handle.Lifetime);

            await handle.RunConcurrentIterations(payload, Process);

            return;

            async Task Process()
            {
                var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                // Task fails once, then succeeds on retry
                queue.Enqueue(new TestPriorityTask(Guid.NewGuid().ToString(),
                    TaskPriority.Medium,
                    failCount: 1,
                    execute: () => {
                        tcs.TrySetResult();
                        return Task.CompletedTask;
                    }));

                await tcs.Task;
                handle.Metrics.Inc();
            }
        }
    }
}