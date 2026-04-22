namespace Infrastructure;

public static class TelemetryPaths
{
    public static string? FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;

        for (var i = 0; i < 10; i++)
        {
            dir = Path.GetDirectoryName(dir);

            if (dir == null)
                return null;

            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
        }

        return null;
    }

    public static string? GetTelemetryDir(string subfolder)
    {
        var root = FindProjectRoot();

        if (root == null)
            return null;

        var dir = Path.Combine(root, ".telemetry", subfolder);
        Directory.CreateDirectory(dir);
        return dir;
    }
}