using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class TransactionSingleTargetTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)]
        public int Iterations { get; set; } = 170;

        [Id(1)]
        public int Concurrent { get; set; } = 10;
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
        public override string Title => "transactions-single-chain";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            var id = Guid.NewGuid();
            await handle.RunConcurrentIterations(payload, () => Process(id));

            return;

            async Task Process(Guid id)
            {
                var grain = _orleans.GetGrain<ITransactionTestGrain>(id);
                var result = await _transactions.Run(() => grain.Increment());

                if (!result.IsSuccess)
                    throw new Exception("Transaction failed");

                handle.Metrics.Inc();
            }
        }
    }
}