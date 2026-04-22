using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class TransactionStateChainedFailTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)]
        public int ChainLength { get; set; } = 3;

        [Id(1)]
        public int Iterations { get; set; } = 140;

        [Id(3)]
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
        public override string Title => "transactions-state-chained-fail";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            await handle.RunConcurrentIterations(payload, () => Process(payload.ChainLength));

            return;

            async Task Process(int chainLength)
            {
                var ids = TestParticipants.Create(_orleans, chainLength);
                var initialState = await ids.Get<int, ITransactionTestGrain>(grain => grain.Get());

                var failResult = await _transactions.Run(async () => {
                    await ids.Run<ITransactionTestGrain>(grain => grain.Increment());
                    throw new Exception("Intentional rollback");
                });

                if (failResult.IsSuccess)
                    throw new Exception("Transaction should have failed but succeeded");

                for (var index = 0; index < ids.Count; index++)
                {
                    var id = ids.Entries[index];
                    var grain = _orleans.GetGrain<ITransactionTestGrain>(id);
                    var value = await grain.Get();
                    var initialValue = initialState[index];

                    if (value != initialValue)
                        throw new Exception($"Rollback failed for grain {id}: expected {initialValue}, got {value}");
                }

                handle.Metrics.Inc();
            }
        }
    }
}