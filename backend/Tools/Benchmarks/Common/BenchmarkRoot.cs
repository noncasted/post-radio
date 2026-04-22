using System.Diagnostics;
using System.Text.Json;
using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

public abstract class BenchmarkRoot<TPayload> : IClusterTest where TPayload : class, new()
{
    protected BenchmarkRoot(ClusterTestUtils utils)
    {
        _utils = utils;
        _payload = new TPayload();
    }

    private readonly ClusterTestUtils _utils;
    private TPayload _payload;

    object IClusterTest.Payload
    {
        get => _payload.ThrowIfNull();
        set => _payload = (TPayload)value;
    }

    BenchmarkResult? IClusterTest.LastResult { get; set; }

    Task IClusterTest.Start(IOperationProgress progress, CancellationToken cancellationToken) =>
        Start(progress, _payload, cancellationToken);

    public abstract string Group { get; }
    public virtual string Subgroup => "";
    public abstract string Title { get; }
    public abstract string MetricName { get; }

    public IMessaging Messaging => _utils.Messaging;
    public IServiceEnvironment Environment => _utils.Environment;
    public ILogger Logger => _utils.Logger;
    public ClusterTestUtils Utils => _utils;
    public TestCleanup Cleanup => _utils.Cleanup;

    public async Task Start(
        IOperationProgress progress,
        TPayload payload,
        CancellationToken cancellationToken = default)
    {
        var lifetime = new Lifetime();
        cancellationToken.Register(() => lifetime.Terminate());
        var handle = new BenchmarkNodeHandle(_utils, progress, lifetime, cancellationToken);
        progress.SetStatus(OperationStatus.Preparing);

        var stopwatch = Stopwatch.StartNew();
        var success = false;
        var cancelled = false;
        var errorMessage = string.Empty;

        try
        {
            var runTask = Run(handle, payload);
            var cancelTask = Task.Delay(Timeout.Infinite, cancellationToken);
            var completed = await Task.WhenAny(runTask, cancelTask);

            if (completed == cancelTask)
                cancelled = true;
            else
                await runTask;

            success = !cancelled && !cancellationToken.IsCancellationRequested;
            cancelled = cancelled || cancellationToken.IsCancellationRequested;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancelled = true;
            Logger.LogInformation("Benchmark {TestName} was cancelled", Title);
        }
        catch (Exception e)
        {
            errorMessage = e.Message;
            progress.Log(e.Message);
            Logger.LogError(e, "Benchmark {TestName} failed with exception", Title);
        }

        stopwatch.Stop();

        var state = handle.Metrics.Collect();
        state.Name = Title;
        state.Success = success;

        if (state.Duration == TimeSpan.Zero)
            state.Duration = stopwatch.Elapsed;

        var metricValue = state.CalculateMetricValue();

        var result = new BenchmarkResult
        {
            BenchmarkName = Title,
            Group = Group,
            MetricName = MetricName,
            MetricValue = metricValue,
            DurationMs = stopwatch.ElapsedMilliseconds,
            PayloadJson = SerializePayload(payload),
            Success = success,
            ErrorMessage = errorMessage
        };

        ((IClusterTest)this).LastResult = result;

        if (!cancelled)
        {
            try
            {
                var baseline = await _utils.BenchmarkStorage.GetBaseline(Title);

                if (baseline != null)
                {
                    var baselineMetric = baseline.CalculateMetricValue();

                    var comparison = BenchmarkComparison.Compare(metricValue, baselineMetric,
                        MetricDirection.HigherIsBetter);
                    state.BaselineMetricValue = comparison.BaselineMetricValue;
                    state.RegressionPercent = comparison.RegressionPercent;
                    state.IsRegression = comparison.IsRegression;

                    if (comparison.IsRegression)
                        Logger.LogWarning(
                            "[BenchmarkRunner] Regression detected for {Title}: {Percent:F1}% vs baseline", Title,
                            comparison.RegressionPercent);
                }

                await _utils.BenchmarkStorage.Write(state);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Benchmark {TestName} failed to save state", Title);
            }
        }

        // Set final status AFTER state is persisted so that UI callbacks
        // (LoadHistory) see the new result in storage
        if (cancelled)
            progress.SetStatus(OperationStatus.Cancelled);
        else if (!string.IsNullOrEmpty(errorMessage))
            progress.SetStatus(OperationStatus.Failed);
        else
            progress.SetStatus(OperationStatus.Success);

        try
        {
            await Cleanup.Execute();
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Benchmark {TestName} cleanup failed", Title);
        }

        lifetime.Terminate();
        await handle.TerminateAllNodes();
    }

    protected abstract Task Run(BenchmarkNodeHandle handle, TPayload payload);

    private static string SerializePayload(TPayload payload)
    {
        try
        {
            return JsonSerializer.Serialize(payload);
        }
        catch
        {
            return "{}";
        }
    }
}