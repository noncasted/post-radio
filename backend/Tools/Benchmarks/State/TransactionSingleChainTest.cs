using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class TransactionSingleChainTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)]
        public int Iterations { get; set; } = 200;

        [Id(1)]
        public int Concurrent { get; set; } = 10;

        [Id(2)]
        public int ChainLength { get; set; } = 3;
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
        public override string Title => "transactions-single-target";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            var ids = TestParticipants.Create(_orleans, payload.ChainLength);
            await handle.RunConcurrentIterations(payload, () => Process(ids));

            return;

            async Task Process(TestParticipants ids)
            {
                var result = await _transactions.Run(() => ids.Run<ITransactionTestGrain>(grain => grain.Increment()));

                if (result.IsSuccess == false)
                    throw new Exception("Chained transaction failed");

                if (!result.IsSuccess)
                    throw new Exception("Transaction failed");

                handle.Metrics.Inc();
            }
        }
    }
}