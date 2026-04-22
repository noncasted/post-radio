using System.Diagnostics.CodeAnalysis;
using Common;
using Common.Extensions;
using Infrastructure;
using Infrastructure.State;

namespace Benchmarks;

public class StateTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)]
        public int Iterations { get; set; } = 3300;

        [Id(1)]
        public int Concurrent { get; set; } = 10;
    }

    [GenerateSerializer]
    [GrainState(Table = "state_test_default_state", State = "state_test", Lookup = "StateTestTest",
        Key = GrainKeyType.String)]
    public class TestState : IStateValue
    {
        [Id(0)]
        public int Inc { get; set; }

        [Id(1)]
        public IGrain Grain { get; set; } = null!;

        [Id(2)]
        public TestStateA A0 { get; set; } = null!;

        public int Version => 0;
    }

    [GenerateSerializer]
    public class TestStateA
    {
        [Id(0)]
        public int A1 { get; set; }

        [Id(1)]
        public IGrain A2 { get; set; } = null!;
    }

    public interface IGrain : IGrainWithStringKey
    {
        Task Test();
    }

    [GrainType("bench-state-test")]
    public class TestGrain : Grain, IGrain
    {
        public TestGrain([State] State<TestState> testState)
        {
            _testState = testState;
        }

        private readonly State<TestState> _testState;

        public async Task Test()
        {
            await _testState.Read();
            var grain2 = GrainFactory.GetGrain<IGrain>("reference-test");

            _testState.Value.Inc += 1;
            _testState.Value.Grain = grain2;

            _testState.Value.A0 = new TestStateA()
            {
                A1 = _testState.Value.Inc + 122,
                A2 = grain2
            };

            await _testState.Write();
        }
    }

    public class Root : BenchmarkRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils, IOrleans orleans) : base(utils)
        {
            _orleans = orleans;
        }

        private readonly IOrleans _orleans;

        public override string Group => TestGroups.State;
        public override string Title => "state";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            await handle.RunConcurrentIterations(payload, Process);

            return;

            async Task Process()
            {
                var grain = _orleans.GetGrain<IGrain>(Guid.NewGuid().ToString());
                await grain.Test();

                handle.Metrics.Inc();
            }
        }
    }
}