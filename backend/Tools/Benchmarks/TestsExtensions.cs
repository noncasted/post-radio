namespace Benchmarks;

public static class TestsExtensions
{
    public static Task RunConcurrentIterations(
        this BenchmarkNodeHandle handle,
        IConcurrentIterationTestPayload payload,
        Func<Task> action)
    {
        return handle.RunConcurrentIterations(payload.Iterations, payload.Concurrent, action);
    }

    public static async Task RunConcurrentIterations(
        this BenchmarkNodeHandle handle,
        int iterations,
        int concurrent,
        Func<Task> action)
    {
        var logInterval = Math.Max(1, iterations / 100);

        for (var i = 0; i < iterations; i++)
        {
            handle.CancellationToken.ThrowIfCancellationRequested();

            var tasks = new List<Task>();

            for (var c = 0; c < concurrent; c++)
                tasks.Add(action());

            await Task.WhenAll(tasks);

            if ((i + 1) % logInterval == 0 || i == iterations - 1)
            {
                var progress = (float)(i + 1) / iterations;
                handle.Progress.SetProgress(progress);
            }
        }
    }
}