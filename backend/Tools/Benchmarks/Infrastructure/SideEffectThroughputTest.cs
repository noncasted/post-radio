using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class SideEffectThroughputTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload()
    {
        [Id(0)]
        public int EffectCount { get; set; } = 1000;
    }

    [GenerateSerializer]
    public class ThroughputSideEffect : ISideEffect
    {
        [Id(0)] public Guid BatchId { get; set; }

        public Task Execute(IOrleans orleans)
        {
            return Task.CompletedTask;
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
        public override string Title => "side-effect-throughput";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var batchId = Guid.NewGuid();
            var total = payload.EffectCount;
            var batchIdStr = batchId.ToString();

            for (var i = 0; i < total; i++)
            {
                handle.Lifetime.Token.ThrowIfCancellationRequested();
                await _storage.Write(new ThroughputSideEffect { BatchId = batchId });
            }

            handle.Progress.Log($"Enqueued {total} effects, waiting for worker...");

            var lastProcessed = 0;

            while (lastProcessed < total)
            {
                handle.Lifetime.Token.ThrowIfCancellationRequested();
                await Task.Delay(50, handle.Lifetime.Token);

                var remaining = await CountRemaining(batchIdStr, handle.Lifetime.Token);
                var processed = total - remaining;
                var delta = processed - lastProcessed;

                for (var i = 0; i < delta; i++)
                    handle.Metrics.Inc();

                lastProcessed = processed;
                handle.Progress.SetProgress((float)lastProcessed / total);
            }

            handle.Progress.Log($"All {total} effects processed");
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
    }
}