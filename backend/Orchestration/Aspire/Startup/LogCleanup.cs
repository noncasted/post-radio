using Infrastructure;

namespace Aspire;

public static class LogCleanup
{
    public static void Run()
    {
        var logsDir = TelemetryPaths.GetTelemetryDir("logs");

        if (logsDir == null)
            return;

        foreach (var entry in Directory.EnumerateFileSystemEntries(logsDir))
        {
            try
            {
                if (File.Exists(entry))
                    File.Delete(entry);
                else if (Directory.Exists(entry))
                    Directory.Delete(entry, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
