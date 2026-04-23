using Common;
using Infrastructure.State;

namespace Tests.Grains;

// --- State versions ---

[GenerateSerializer]
[GrainState(Table = "state_test_default_state", State = "migration_test_state", Lookup = "StateMigrationTest",
    Key = GrainKeyType.String)]
public class MigrationTestState_0 : IStateValue
{
    [Id(0)] public int Value { get; set; }
    public int Version => 0;
}

[GenerateSerializer]
[GrainState(Table = "state_test_default_state", State = "migration_test_state", Lookup = "StateMigrationTest",
    Key = GrainKeyType.String)]
public class MigrationTestState_1 : IStateValue
{
    [Id(0)] public int Value { get; set; }
    [Id(1)] public string Label { get; set; } = string.Empty;
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
        return _serializer.TryDeserialize<MigrationTestState_0>(raw)!;
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
        return _serializer.TryDeserialize<MigrationTestState_1>(raw)!;
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

// --- V2 state ---

[GenerateSerializer]
[GrainState(Table = "state_test_default_state", State = "migration_test_state", Lookup = "StateMigrationTest",
    Key = GrainKeyType.String)]
public class MigrationTestState_2 : IStateValue
{
    [Id(0)] public int Value { get; set; }
    [Id(1)] public string Label { get; set; } = string.Empty;
    [Id(2)] public int DoubledValue { get; set; }
    public int Version => 2;
}

// --- V2 migration step ---

public class MigrationTestStep_V2 : IStateMigrationStep
{
    public int Version => 2;
    public Type Type => typeof(MigrationTestState_2);

    public IStateValue Deserialize(string raw)
    {
        throw new NotSupportedException("V2 is the latest version; deserialize goes through V1.");
    }

    public IStateValue Migrate(IStateValue value)
    {
        var v1 = (MigrationTestState_1)value;

        return new MigrationTestState_2
        {
            Value = v1.Value,
            Label = v1.Label,
            DoubledValue = v1.Value * 2
        };
    }
}

// --- V2 migration steps that target MigrationTestState_2 ---
// We need steps for V0 and V1 that target MigrationTestState_2 (the new final type)

public class MigrationV2TestStep_V0 : IStateMigrationStep
{
    public MigrationV2TestStep_V0(IStateSerializer serializer)
    {
        _serializer = serializer;
    }

    private readonly IStateSerializer _serializer;

    public int Version => 0;
    public Type Type => typeof(MigrationTestState_2);

    public IStateValue Deserialize(string raw)
    {
        return _serializer.TryDeserialize<MigrationTestState_0>(raw)!;
    }

    public IStateValue Migrate(IStateValue value)
    {
        throw new NotSupportedException("V0 step is never a migration target.");
    }
}

public class MigrationV2TestStep_V1 : IStateMigrationStep
{
    public MigrationV2TestStep_V1(IStateSerializer serializer)
    {
        _serializer = serializer;
    }

    private readonly IStateSerializer _serializer;

    public int Version => 1;
    public Type Type => typeof(MigrationTestState_2);

    public IStateValue Deserialize(string raw)
    {
        return _serializer.TryDeserialize<MigrationTestState_1>(raw)!;
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

public interface IMigrationGrainV2 : IGrainWithStringKey
{
    Task<(int value, string label, int doubledValue)> Read();
}

public class MigrationGrainV2 : Grain, IMigrationGrainV2
{
    public MigrationGrainV2([State] State<MigrationTestState_2> state)
    {
        _state = state;
    }

    private readonly State<MigrationTestState_2> _state;

    public async Task<(int value, string label, int doubledValue)> Read()
    {
        await _state.Read();
        return (_state.Value.Value, _state.Value.Label, _state.Value.DoubledValue);
    }
}