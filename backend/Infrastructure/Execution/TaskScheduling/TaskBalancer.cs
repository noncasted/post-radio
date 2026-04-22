using System.Diagnostics;
using System.Text;
using Common.Extensions;
using Common.Reactive;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Execution;

public interface ITaskBalancer
{
    Task Run(IReadOnlyLifetime lifetime);
}

public class TaskBalancer : ITaskBalancer
{
    public TaskBalancer(ITaskQueue queue, ILogger<TaskBalancer> logger, ITaskBalancerConfig config)
    {
        _queue = queue;
        _logger = logger;
        _config = config;
    }

    private readonly ILogger<TaskBalancer> _logger;
    private readonly ITaskBalancerConfig _config;
    private readonly ITaskQueue _queue;

    private readonly IReadOnlyDictionary<TaskPriority, int> _priorityToScore = new Dictionary<TaskPriority, int>
    {
        [TaskPriority.Low] = 10,
        [TaskPriority.Medium] = 20,
        [TaskPriority.High] = 30,
        [TaskPriority.Critical] = 40
    };

    private readonly Dictionary<string, TaskEntry> _scheduled = new();
    private readonly Lock _scheduledLock = new();

    public Task Run(IReadOnlyLifetime lifetime)
    {
        CollectLoop(lifetime).NoAwait();
        ExecuteLoop(lifetime).NoAwait();
        return Task.CompletedTask;
    }

    private async Task CollectLoop(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            try
            {
                var options = _config.Value;
                var items = _queue.Collect();

                if (items.Count == 0)
                {
                    await Task.Delay(options.EmptyDelayMs);
                    continue;
                }

                lock (_scheduledLock)
                {
                    var incoming = new HashSet<string>(items.Count);

                    foreach (var task in items)
                        incoming.Add(task.Id);

                    foreach (var (_, entry) in _scheduled)
                    {
                        if (incoming.Contains(entry.Key) == false)
                            entry.AddScore(options.IterationScore);
                    }

                    foreach (var task in items)
                    {
                        if (_scheduled.TryGetValue(task.Id, out var entry))
                        {
                            entry.AddScore(options.IterationScore);
                            continue;
                        }

                        var newEntry = new TaskEntry
                        {
                            Task = task,
                            Key = task.Id,
                            InitialScore = _priorityToScore[task.Priority]
                        };
                        newEntry.Init();
                        _scheduled[task.Id] = newEntry;
                    }
                }

                LogEntries();

                await Task.Delay(options.NextDelayMs);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[TaskBalancer] CollectLoop iteration failed");
                await Task.Delay(_config.Value.EmptyDelayMs);
            }
        }

        void LogEntries()
        {
            var sb = new StringBuilder();

            lock (_scheduledLock)
            {
                sb.Append($"[TaskBalancer] Currently scheduled tasks ({_scheduled.Count}):\n");

                foreach (var (_, entry) in _scheduled)
                    sb.AppendLine($"    {entry.Task.Id} with score {entry.Score}");
            }

            _logger.LogTrace(sb.ToString());
        }
    }

    private async Task ExecuteLoop(IReadOnlyLifetime lifetime)
    {
        // ConcurrentTasks is captured once — changing it requires a restart
        var concurrentTasks = _config.Value.ConcurrentTasks;
        var executionLock = new SemaphoreSlim(concurrentTasks, concurrentTasks);

        while (lifetime.IsTerminated == false)
        {
            var acquired = false;

            try
            {
                var options = _config.Value;

                await executionLock.WaitAsync();
                acquired = true;

                if (TryPickMaxScored(out var entry) == false)
                {
                    executionLock.Release();
                    acquired = false;
                    BackendMetrics.TaskQueueDepth.Record(0);
                    await Task.Delay(options.EmptyDelayMs);
                    continue;
                }

                lock (_scheduledLock)
                {
                    BackendMetrics.TaskQueueDepth.Record(_scheduled.Count);
                }

                acquired = false;
                Execute(entry!).NoAwait();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[TaskBalancer] ExecuteLoop iteration failed");

                if (acquired)
                    executionLock.Release();

                await Task.Delay(_config.Value.EmptyDelayMs);
            }
        }

        return;

        async Task Execute(TaskEntry entry)
        {
            using var activity = TraceExtensions.TaskBalancer.StartActivity("TaskBalancer.Execute");
            activity?.SetTag("task.priority", entry.Task.Priority.ToString());
            activity?.SetTag("task.score", entry.Score);

            using var watch = MetricWatch.Start(BackendMetrics.TaskDuration);
            var stopwatch = Stopwatch.StartNew();
            var success = false;

            try
            {
                _logger.LogTrace("[TaskBalancer] Executing task, free handles: {Count}, taskId: {TaskId}",
                    executionLock.CurrentCount,
                    entry.Task.Id);

                await entry.Task.Execute();
                success = true;
            }
            catch (Exception e)
            {
                stopwatch.Stop();
                activity?.SetStatus(ActivityStatusCode.Error, e.Message);
                entry.AddScore(-_config.Value.ExceptionPenalty);
                _queue.Enqueue(entry.Task);

                _logger.LogError(e,
                    "[TaskBalancer] Task execution failed in {Time}, taskId: {TaskId}",
                    stopwatch.Elapsed,
                    entry.Task.Id);
            }
            finally
            {
                executionLock.Release();
                BackendMetrics.TaskExecuted.Add(1);

                if (success)
                    BackendMetrics.TaskSuccess.Add(1);
                else
                    BackendMetrics.TaskFailure.Add(1);
            }

            if (success)
            {
                stopwatch.Stop();

                _logger.LogTrace("[TaskBalancer] Task execution completed in {Time}, taskId: {TaskId}",
                    stopwatch.Elapsed,
                    entry.Task.Id);
            }
        }
    }

    private bool TryPickMaxScored(out TaskEntry? maxEntry)
    {
        maxEntry = null;

        lock (_scheduledLock)
        {
            foreach (var (_, entry) in _scheduled)
                if (maxEntry == null || entry.Score > maxEntry.Score)
                    maxEntry = entry;

            if (maxEntry == null)
                return false;

            _scheduled.Remove(maxEntry.Key);
        }

        return true;
    }

    private class TaskEntry
    {
        public required string Key { get; init; }
        public required IPriorityTask Task { get; init; }
        public required int InitialScore { get; init; }

        private int _score;
        public int Score => _score;

        public void AddScore(int value) => Interlocked.Add(ref _score, value);

        public TaskEntry() => _score = 0;

        public void Init() => _score = InitialScore;
    }
}