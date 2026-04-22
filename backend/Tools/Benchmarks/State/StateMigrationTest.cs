using Common;
using Common.Extensions;
using Infrastructure.State;

namespace Benchmarks;

public class StateMigrationTest
{
    // --- State versions ---

    [GenerateSerializer]
    [GrainState(Table = "state_test_default_state", State = "migration_test_state", Lookup = "StateMigrationTest",
        Key = GrainKeyType.String)]
    public class MigrationTestState_0 : IStateValue
    {
        [Id(0)]
        public int Value { get; set; }

        public int Version => 0;
    }

    [GenerateSerializer]
    [GrainState(Table = "state_test_default_state", State = "migration_test_state", Lookup = "StateMigrationTest",
        Key = GrainKeyType.String)]
    public class MigrationTestState_1 : IStateValue
    {
        [Id(0)]
        public int Value { get; set; }

        [Id(1)]
        public string Label { get; set; } = string.Empty;

        public int Version => 1;
    }

    // --- Migration steps ---

    public class MigrationTestStep_V0 : IStateMigrationStep
    {
        public MigrationTestStep_V0(IStateSerializer serializer)
        {
            _serializer = serializer;
        }

        private readonly IStateSerializer _serializer;

        public int Version => 0;
        public Type Type => typeof(MigrationTestState_1);

        public IStateValue Deserialize(string raw)
        {
            return _serializer.TryDeserialize<MigrationTestState_0>(raw).ThrowIfNull();
        }

        public IStateValue Migrate(IStateValue value)
        {
            throw new NotSupportedException("V0 step is never a migration target.");
        }
    }

    public class MigrationTestStep_V1 : IStateMigrationStep
    {
        public MigrationTestStep_V1(IStateSerializer serializer)
        {
            _serializer = serializer;
        }

        private readonly IStateSerializer _serializer;

        public int Version => 1;
        public Type Type => typeof(MigrationTestState_1);

        public IStateValue Deserialize(string raw)
        {
            return _serializer.TryDeserialize<MigrationTestState_1>(raw).ThrowIfNull();
        }

        public IStateValue Migrate(IStateValue value)
        {
            var v0 = (MigrationTestState_0)value;

            return new MigrationTestState_1
            {
                Value = v0.Value,
                Label = $"migrated-{v0.Value}"
            };
        }
    }

    // --- Grains ---

    public interface IMigrationGrainV0 : IGrainWithStringKey
    {
        Task Write(int value);
    }

    [GrainType("bench-migration-v0")]
    public class MigrationGrainV0 : Grain, IMigrationGrainV0
    {
        public MigrationGrainV0([State] State<MigrationTestState_0> state)
        {
            _state = state;
        }

        private readonly State<MigrationTestState_0> _state;

        public Task Write(int value)
        {
            return _state.Update(s => s.Value = value);
        }
    }

    public interface IMigrationGrainV1 : IGrainWithStringKey
    {
        Task<(int value, string label)> Read();
    }

    [GrainType("bench-migration-v1")]
    public class MigrationGrainV1 : Grain, IMigrationGrainV1
    {
        public MigrationGrainV1([State] State<MigrationTestState_1> state)
        {
            _state = state;
        }

        private readonly State<MigrationTestState_1> _state;

        public async Task<(int value, string label)> Read()
        {
            await _state.Read();
            return (_state.Value.Value, _state.Value.Label);
        }
    }

    [GenerateSerializer]
    public class EmptyPayload
    {
    }
}