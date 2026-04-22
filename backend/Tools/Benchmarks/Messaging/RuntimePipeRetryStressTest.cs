using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class RuntimePipeRetryStressTest
{
    [GenerateSerializer]
    public class PipeRequest
    {
        [Id(0)] public int Index { get; set; }
    }

    [GenerateSerializer]
    public class PipeResponse
    {
        [Id(0)] public int Index { get; set; }
    }

    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload()
    {
        [Id(0)]
        public int RequestCount { get; set; } = 500;

        [Id(1)]
        public double FailureRate { get; set; } = 0.2;

        [Id(2)]
        public int Concurrency { get; set; } = 32;
    }

    public static string TestName => "runtime-pipe-retry-stress";

    public class Root : BenchmarkRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils) : base(utils)
        {
        }

        public override string Group => TestGroups.Messaging;
        public override string Subgroup => TestGroups.Subgroups.RuntimePipe;
        public override string Title => "Retry stress (intermittent failures)";
        public override string MetricName => "req/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var pipeId = new RuntimePipeId(TestName);
            var totalRequests = payload.RequestCount;
            var failureRate = payload.FailureRate;

            var handlerInvocations = 0;
            var handlerFailures = 0;
            var successCount = 0;
            var failedCount = 0;
            var completedCount = 0;

            await Messaging.AddPipeRequestHandler<PipeRequest, PipeResponse>(handle.Lifetime, pipeId, req => {
                Interlocked.Increment(ref handlerInvocations);

                if (Random.Shared.NextDouble() < failureRate)
                {
                    Interlocked.Increment(ref handlerFailures);
                    throw new Exception("Transient failure");
                }

                return Task.FromResult(new PipeResponse { Index = req.Index });
            });

            handle.Progress.Log(
                $"Handler ready (failure rate: {failureRate:P0}). Sending {totalRequests} requests (concurrency: {payload.Concurrency})...");

            var logStep = Math.Max(1, totalRequests / 20);

            using var semaphore = new SemaphoreSlim(payload.Concurrency);

            var tasks = Enumerable.Range(0, totalRequests).Select(async i => {
                await semaphore.WaitAsync(handle.CancellationToken);

                try
                {
                    await Messaging.SendPipe<PipeResponse>(pipeId, new PipeRequest { Index = i });
                    Interlocked.Increment(ref successCount);
                    handle.Metrics.Inc();
                }
                catch
                {
                    Interlocked.Increment(ref failedCount);
                }
                finally
                {
                    semaphore.Release();

                    var completed = Interlocked.Increment(ref completedCount);

                    if (completed % logStep == 0 || completed == totalRequests)
                    {
                        handle.Progress.SetProgress((float)completed / totalRequests);

                        handle.Progress.Log($"Progress: {completed}/{totalRequests}, " +
                                            $"success: {successCount}, exhausted: {failedCount}, " +
                                            $"handler invocations: {handlerInvocations}, handler failures: {handlerFailures}");
                    }
                }
            });

            await Task.WhenAll(tasks);

            var amplification = totalRequests > 0 ? (double)handlerInvocations / totalRequests : 0;

            handle.Progress.Log($"Done. Success: {successCount}/{totalRequests}, exhausted retries: {failedCount}. " +
                                $"Handler invocations: {handlerInvocations} (amplification: {amplification:F2}x), " +
                                $"handler failures: {handlerFailures}.");

            handle.Progress.SetProgress(1f);
        }
    }
}