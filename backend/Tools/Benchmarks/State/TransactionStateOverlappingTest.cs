using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class TransactionStateOverlappingTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)]
        public int ChainLength { get; set; } = 3;

        [Id(1)]
        public int Iterations { get; set; } = 1100;

        [Id(2)]
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
        public override string Title => "transactions-state-overlapping";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            await handle.RunConcurrentIterations(payload, () => Process(payload.ChainLength));

            return;

            async Task Process(int chainLength)
            {
                var ids = TestParticipants.Create(_orleans, chainLength);

                var taskA = _transactions.Run(() => ids.Run<ITransactionTestGrain>(grain => grain.Increment()));
                var taskB = _transactions.Run(() => ids.Run<ITransactionTestGrain>(grain => grain.Increment()));

                var resultA = await taskA;
                var resultB = await taskB;

                if (resultA.IsSuccess == false || resultB.IsSuccess == false)
                    throw new Exception("Chained transaction failed");

                handle.Metrics.Inc();
            }
        }
    }
}