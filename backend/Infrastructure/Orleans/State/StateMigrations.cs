namespace Infrastructure.State;

public interface IStateMigrationStep
{
    int Version { get; }
    Type Type { get; }

    IStateValue Migrate(IStateValue value);
    IStateValue Deserialize(string raw);
}

public interface IStateMigrations
{
    int GetLatestVersion<T>() where T : IStateValue;
    T Migrate<T>(string raw, int currentVersion) where T : IStateValue, new();
}

public class StateMigrations : IStateMigrations
{
    public StateMigrations(IEnumerable<IStateMigrationStep> allSteps)
    {
        var migrationsByType = new Dictionary<Type, Dictionary<int, IStateMigrationStep>>();

        foreach (var step in allSteps)
        {
            var type = step.Type;

            if (migrationsByType.TryGetValue(type, out var map) == false)
            {
                map = new Dictionary<int, IStateMigrationStep>();
                migrationsByType.Add(type, map);
            }

            map[step.Version] = step;
        }

        var readOnlyMigrationsByType = new Dictionary<Type, IReadOnlyDictionary<int, IStateMigrationStep>>();

        foreach (var (type, map) in migrationsByType)
            readOnlyMigrationsByType[type] = map;

        _migrationsByType = readOnlyMigrationsByType;

        foreach (var (type, steps) in readOnlyMigrationsByType)
            _currentVersions[type] = steps.Max(t => t.Value.Version);
    }

    private readonly Dictionary<Type, IReadOnlyDictionary<int, IStateMigrationStep>> _migrationsByType;
    private readonly Dictionary<Type, int> _currentVersions = new();

    public int GetLatestVersion<T>() where T : IStateValue
    {
        var type = typeof(T);

        if (_currentVersions.TryGetValue(type, out var version) == false)
            return 0;

        return version;
    }

    public T Migrate<T>(string raw, int currentVersion) where T : IStateValue, new()
    {
        var type = typeof(T);

        if (_migrationsByType.TryGetValue(type, out var migrations) == false)
            throw new Exception($"No migrations found for type {type.FullName}.");

        var value = migrations[currentVersion].Deserialize(raw);

        while (migrations.ContainsKey(currentVersion + 1) == true)
        {
            currentVersion++;
            var migration = migrations[currentVersion];
            value = migration.Migrate(value);
        }

        if (value is not T resultValue)
        {
            throw new Exception(
                $"Incorrect result type after migration {type.FullName}. Result type: {value.GetType().FullName}.");
        }

        return resultValue;
    }
}