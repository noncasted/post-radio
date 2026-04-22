using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class SideEffectDeadLetterThroughputTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload()
    {
        [Id(0)]
        public int EffectCount { get; set; } = 500;
    }

    [GenerateSerializer]
    public class AlwaysFailingBenchEffect : ISideEffect
    {
        [Id(0)] public Guid BatchId { get; set; }

        public Task Execute(IOrleans orleans)
        {
            throw new Exception($"Benchmark failure: {BatchId}");
        }
    }

    public class Root : BenchmarkRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils, ISideEffectsStorage storage, IDbSource dbSource) : base(utils)
        {
            _storage = storage;
            _dbSource = dbSource;
        }

        private readonly ISideEffectsStorage _storage;
        private readonly IDbSource _dbSource;

        public override string Group => TestGroups.Infrastructure;
        public override string Title => "side-effect-dead-letter-throughput";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var batchId = Guid.NewGuid();
            var total = payload.EffectCount;
            var batchIdStr = batchId.ToString();

            handle.Progress.Log($"Enqueuing {total} always-failing effects...");

            for (var i = 0; i < total; i++)
            {
                handle.Lifetime.Token.ThrowIfCancellationRequested();
                await _storage.Write(new AlwaysFailingBenchEffect { BatchId = batchId });
            }

            handle.Progress.Log($"Enqueued {total}. Waiting for worker to exhaust retries → dead letter...");

            var lastDeadLetter = 0;

            while (true)
            {
                handle.Lifetime.Token.ThrowIfCancellationRequested();
                await Task.Delay(200, handle.Lifetime.Token);

                await _storage.RequeueReady();

                var remaining = await CountRemaining(batchIdStr, handle.Lifetime.Token);
                var deadLetterCount = await CountDeadLetter(batchIdStr, handle.Lifetime.Token);
                var delta = deadLetterCount - lastDeadLetter;

                for (var i = 0; i < delta; i++)
                    handle.Metrics.Inc();

                lastDeadLetter = deadLetterCount;
                handle.Progress.SetProgress((float)deadLetterCount / total);
                handle.Progress.Log($"Dead letter: {deadLetterCount}/{total}, remaining: {remaining}");

                if (remaining == 0)
                    break;
            }

            handle.Progress.Log($"Done. All {lastDeadLetter} effects moved to dead letter.");
        }

        private async Task<int> CountRemaining(string batchId, CancellationToken ct)
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    (SELECT count(*) FROM side_effects_queue WHERE payload->>'BatchId' = @bid) +
                    (SELECT count(*) FROM side_effects_processing WHERE payload->>'BatchId' = @bid) +
                    (SELECT count(*) FROM side_effects_retry_queue WHERE payload->>'BatchId' = @bid)
            ";
            command.Parameters.AddWithValue("bid", batchId);
            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt32(result);
        }

        private async Task<int> CountDeadLetter(string batchId, CancellationToken ct)
        {
            await using var connection = await _dbSource.Value.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT count(*) FROM side_effects_dead_letter WHERE payload->>'BatchId' = @bid
            ";
            command.Parameters.AddWithValue("bid", batchId);
            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt32(result);
        }
    }
}