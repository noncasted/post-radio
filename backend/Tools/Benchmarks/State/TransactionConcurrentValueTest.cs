using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class TransactionConcurrentValueTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)]
        public int Iterations { get; set; } = 550;

        [Id(1)]
        public int Concurrent { get; set; } = 10;

        [Id(2)]
        public int ConcurrentTransactions { get; set; } = 5;
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
        public override string Title => "transactions-concurrent-value";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            await handle.RunConcurrentIterations(payload, () => Process(payload.ConcurrentTransactions));

            return;

            async Task Process(int concurrentTxns)
            {
                var id = Guid.NewGuid();
                var grain = _orleans.GetGrain<ITransactionTestGrain>(id);

                var tasks = new List<Task<TransactionResult>>();

                for (var i = 0; i < concurrentTxns; i++)
                    tasks.Add(_transactions.Run(() => grain.Increment()));

                var results = await Task.WhenAll(tasks);

                var successCount = results.Count(r => r.IsSuccess);

                if (successCount == 0)
                    throw new Exception("All concurrent transactions failed");

                handle.Metrics.Inc();
            }
        }
    }
}