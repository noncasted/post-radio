using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class TransactionCrossPathReadTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)]
        public int Iterations { get; set; } = 2000;

        [Id(1)]
        public int Concurrent { get; set; } = 3;
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
        public override string Title => "transactions-cross-path-read";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            await handle.RunConcurrentIterations(payload, Process);

            return;

            async Task Process()
            {
                var id = Guid.NewGuid();
                var grain = _orleans.GetGrain<ITransactionTestGrain>(id);

                // Write via transaction
                var result = await _transactions.Run(() => grain.Increment());

                if (!result.IsSuccess)
                    throw new Exception("Transaction failed");

                // Read via non-transactional path
                var value = await grain.Get();

                if (value != 1)
                    throw new Exception($"Cross-path read mismatch: expected 1, got {value}");

                handle.Metrics.Inc();
            }
        }
    }
}