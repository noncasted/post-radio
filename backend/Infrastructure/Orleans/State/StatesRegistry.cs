using Common;

namespace Infrastructure.State;

public class GrainStateInfo
{
    public required string TableName { get; init; }
    public required GrainKeyType KeyType { get; init; }
    public required Type Type { get; init; }
    public required string Name { get; init; }
}

public interface IGrainStatesRegistry
{
    IReadOnlyCollection<GrainStateInfo> All { get; }
    GrainStateInfo Get<T>();
    GrainStateInfo Get(Type type);
}

public class GrainStatesRegistry : IGrainStatesRegistry
{
    public GrainStatesRegistry(ICollection<GrainStateInfo> statesInfo)
    {
        var states = new Dictionary<Type, GrainStateInfo>(statesInfo.Count);

        foreach (var stateInfo in statesInfo)
            states.Add(stateInfo.Type, stateInfo);

        _states = states;
    }

    private readonly Dictionary<Type, GrainStateInfo> _states;

    public IReadOnlyCollection<GrainStateInfo> All => _states.Values;

    public GrainStateInfo Get<T>()
    {
        var type = typeof(T);
        return _states[type];
    }

    public GrainStateInfo Get(Type type)
    {
        return _states[type];
    }
}