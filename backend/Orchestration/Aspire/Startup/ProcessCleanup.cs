using System.Diagnostics;

namespace Aspire;

public static class ProcessCleanup
{
    public static void Run()
    {
        var currentPid = Environment.ProcessId;
        var currentName = Process.GetCurrentProcess().ProcessName;

        var targets = Process.GetProcessesByName(currentName)
                             .Where(p => p.Id != currentPid)
                             .ToList();

        targets.AddRange(Process.GetProcessesByName("Silo"));
        targets.AddRange(Process.GetProcessesByName("MetaGateway"));
        targets.AddRange(Process.GetProcessesByName("GameGateway"));
        targets.AddRange(Process.GetProcessesByName("ConsoleGateway"));
        targets.AddRange(Process.GetProcessesByName("Coordinator"));

        Parallel.ForEach(targets, process => {
            try
            {
                Console.WriteLine($"[ProcessCleanup] Killing old process: {process.ProcessName} (pid={process.Id})");
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessCleanup] Failed to kill pid={process.Id}: {ex.Message}");
            }
        });
    }
}