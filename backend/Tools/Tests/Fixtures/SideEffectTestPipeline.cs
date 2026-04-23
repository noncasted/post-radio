using System.Diagnostics;
using Infrastructure;

namespace Tests.Fixtures;

/// <summary>
/// Controls the side effects execution cycle in tests.
/// Replaces SideEffectsWorker with synchronous pump/drain semantics.
/// </summary>
public class SideEffectTestPipeline
{
    public SideEffectTestPipeline(
        ISideEffectsStorage storage,
        ITransactions transactions,
        IOrleans orleans,
        ISideEffectsConfig config)
    {
        _storage = storage;
        _transactions = transactions;
        _orleans = orleans;
        _config = config;
    }

    private readonly ISideEffectsStorage _storage;
    private readonly ITransactions _transactions;
    private readonly IOrleans _orleans;
    private readonly ISideEffectsConfig _config;

    /// <summary>
    /// Execute one cycle: requeue ready retries, fetch from queue, execute each.
    /// Returns the number of entries processed.
    /// </summary>
    public async Task<PumpResult> PumpOnceAsync()
    {
        await _storage.RequeueReady();

        var entries = await _storage.Read(_config.Value.ConcurrentExecutions);

        if (entries.Count == 0)
            return PumpResult.Empty;

        var results = new List<BatchExecutionInfo>();

        foreach (var entry in entries)
        {
            var info = await ExecuteEntry(entry);
            results.Add(info);
        }

        return new PumpResult(results);
    }

    private async Task<BatchExecutionInfo> ExecuteEntry(SideEffectEntry entry)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            if (entry.Effect is ITransactionalSideEffect)
            {
                var result = await _transactions
                                   .CreateBuilder(() => entry.Effect.Execute(_orleans))
                                   .WithCallback(tx => _storage.CompleteProcessing(tx, entry.Id))
                                   .Run();

                if (!result.IsSuccess)
                    throw new Exception("Transactional side effect failed");
            }
            else
            {
                await entry.Effect.Execute(_orleans);
                await _storage.CompleteProcessing(entry.Id);
            }

            sw.Stop();
            return new BatchExecutionInfo(entry.Id, true, sw.Elapsed, null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var options = _config.Value;

            await _storage.FailProcessing(entry.Id,
                entry.RetryCount,
                options.MaxRetryCount,
                options.IncrementalRetryDelay);
            return new BatchExecutionInfo(entry.Id, false, sw.Elapsed, ex.Message);
        }
    }

    /// <summary>
    /// Settle — wait for SE to appear in queue, then drain until quiet.
    /// </summary>
    public async Task<DrainResult> SettleAndDrainAsync(int settleTimeoutMs = 500, int maxIterations = 50)
    {
        // Adaptive settle: poll until queue has something or timeout
        var settleStart = DateTime.UtcNow;

        while ((DateTime.UtcNow - settleStart).TotalMilliseconds < settleTimeoutMs)
        {
            await _storage.RequeueReady();
            var peek = await _storage.Read(1);

            if (peek.Count > 0)
            {
                // Put it back (we just want to peek)
                // Actually Read() moves to processing, so we need to execute it
                // Let's just proceed to drain
                var firstResult = await ExecuteEntry(peek[0]);
                return await DrainUntilQuietAsync(maxIterations, [firstResult]);
            }

            await Task.Delay(10);
        }

        return DrainResult.Quiet;
    }

    /// <summary>
    /// Pump until no more work is found.
    /// </summary>
    public async Task<DrainResult> DrainUntilQuietAsync(
        int maxIterations = 50,
        List<BatchExecutionInfo>? initial = null)
    {
        var allResults = initial ?? [];
        var iterations = 0;

        while (iterations < maxIterations)
        {
            var result = await PumpOnceAsync();

            if (result.IsEmpty)
                break;

            allResults.AddRange(result.Batches);
            iterations++;
        }

        return new DrainResult(allResults, iterations < maxIterations);
    }

    /// <summary>
    /// Pump until a condition is met.
    /// </summary>
    public async Task<DrainResult> PumpUntilAsync(Func<bool> condition, int maxIterations = 50)
    {
        var allResults = new List<BatchExecutionInfo>();

        for (var i = 0; i < maxIterations && !condition(); i++)
        {
            var result = await PumpOnceAsync();
            allResults.AddRange(result.Batches);

            if (result.IsEmpty)
                await Task.Delay(50);
        }

        return new DrainResult(allResults, condition());
    }
}

// --- Result types ---

public record PumpResult(IReadOnlyList<BatchExecutionInfo> Batches)
{
    public static PumpResult Empty { get; } = new(Array.Empty<BatchExecutionInfo>());

    public bool IsEmpty => Batches.Count == 0;
    public int TotalTasks => Batches.Count;
    public bool AllSucceeded => Batches.All(b => b.Success);
}

public record BatchExecutionInfo(Guid Id, bool Success, TimeSpan Duration, string? ErrorMessage)
{
    public string? ShortError => ErrorMessage?.Split('\n').FirstOrDefault();
}

public record DrainResult(IReadOnlyList<BatchExecutionInfo> ExecutionTrace, bool ReachedQuiescence)
{
    public static DrainResult Quiet { get; } = new(Array.Empty<BatchExecutionInfo>(), true);

    public int TotalTasks => ExecutionTrace.Count;
    public bool AllSucceeded => ExecutionTrace.All(e => e.Success);
}

// --- Assertion extensions ---

public static class DrainResultExtensions
{
    public static void AssertDrainedSuccessfully(this DrainResult result)
    {
        if (!result.ReachedQuiescence)
            throw new Exception("Drain did not reach quiescence");

        if (!result.AllSucceeded)
            throw new Exception($"Drain had failures: {FormatTrace(result)}");
    }

    public static void AssertDrainedWithWork(this DrainResult result)
    {
        result.AssertDrainedSuccessfully();

        if (result.TotalTasks == 0)
            throw new Exception("Drain completed but no work was executed");
    }

    public static void AssertAllSucceeded(this DrainResult result)
    {
        if (!result.AllSucceeded)
            throw new Exception($"Not all tasks succeeded: {FormatTrace(result)}");
    }

    private static string FormatTrace(DrainResult result)
    {
        var failures = result.ExecutionTrace.Where(e => !e.Success).ToList();
        return string.Join("\n", failures.Select(f => $"  [{f.Id}] {f.ShortError}"));
    }
}