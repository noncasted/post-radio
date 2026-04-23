using Common.Extensions;

namespace Tests.Fixtures;

/// <summary>
/// Loads config JSON files from Orchestration/Coordinator directory.
/// Uses the same JsonUtils.Deserialize (Newtonsoft with TypeNameHandling.All)
/// as ClusterConfigsSetup does in production.
/// </summary>
public static class ConfigLoader
{
    private static readonly Lazy<string> ConfigDirectory = new(FindConfigDirectory);

    /// <summary>
    /// Load a config from its JSON file, e.g. "config.bot" → config.bot.json
    /// Falls back to new T() if file not found.
    /// </summary>
    public static T Load<T>(string configName) where T : class, new()
    {
        var path = Path.Combine(ConfigDirectory.Value, $"{configName}.json");

        if (!File.Exists(path))
            return new T();

        var json = File.ReadAllText(path);
        return JsonUtils.Deserialize<T>(json)!;
    }

    private static string FindConfigDirectory()
    {
        // Walk up from test assembly directory until we find the backend folder
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, "backend", "Orchestration", "Coordinator");

            if (Directory.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        // Fallback: try relative from working directory
        var fallback = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "Orchestration", "Coordinator"));

        if (Directory.Exists(fallback))
            return fallback;

        throw new DirectoryNotFoundException("Could not find Orchestration/Coordinator config directory. " +
                                             $"Searched from: {AppContext.BaseDirectory}");
    }
}