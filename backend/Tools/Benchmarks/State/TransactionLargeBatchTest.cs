using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class TransactionLargeBatchTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)]
        public int Iterations { get; set; } = 450;

        [Id(1)]
        public int Concurrent { get; set; } = 5;

        [Id(2)]
        public int GrainCount { get; set; } = 20;
    }

    public class Root : BenchmarkRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils, IOrleans orleans, ITransactions transactions) : base(utils)
        {
            _orleans = orleans;
            _transactions = transactions;
        }

        private readonly IOrleans _orleans;
        private readonly ITransactions _transactions;

        public override string Group => TestGroups.State;
        public override string Title => "transactions-large-batch";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            await handle.RunConcurrentIterations(payload, () => Process(payload.GrainCount));

            return;

            async Task Process(int grainCount)
            {
                var ids = TestParticipants.Create(_orleans, grainCount);

                var result = await _transactions.Run(() => ids.Run<ITransactionTestGrain>(grain => grain.Increment()));

                if (!result.IsSuccess)
                    throw new Exception($"Large batch transaction with {grainCount} grains failed");

                handle.Metrics.Inc();
            }
        }
    }
}