using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Common;
using Microsoft.Extensions.Logging;

namespace Infrastructure.TaskScheduling;

public interface ITaskBalancer
{
    Task Run(IReadOnlyLifetime lifetime);
}

public class TaskBalancer : ITaskBalancer
{
    public TaskBalancer(ITaskQueue queue, ILogger<TaskBalancer> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    private readonly IReadOnlyDictionary<TaskPriority, int> _priorityToScore = new Dictionary<TaskPriority, int>
    {
        [TaskPriority.Low] = 10,
        [TaskPriority.Medium] = 20,
        [TaskPriority.High] = 30,
        [TaskPriority.Critical] = 40
    };

    private const int _iterationScore = 1;
    private const int _exceptionPenalty = 50;
    private const int _concurrentTasks = 10;

    private readonly ILogger<TaskBalancer> _logger;
    private readonly ITaskQueue _queue;
    private readonly TimeSpan _emptyDelay = TimeSpan.FromMilliseconds(500);
    private readonly TimeSpan _nextDelay = TimeSpan.FromMilliseconds(100);

    private readonly ConcurrentDictionary<string, TaskEntry> _scheduled = new();

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
            var items = _queue.Collect();

            if (items.Count == 0)
            {
                await Task.Delay(_emptyDelay);
                continue;
            }

            foreach (var (_, entry) in _scheduled)
                entry.Score += _iterationScore;

            foreach (var task in items)
            {
                if (_scheduled.TryGetValue(task.Id, out var entry) == true)
                {
                    entry.Score += _iterationScore;
                    continue;
                }

                _scheduled[task.Id] = new TaskEntry
                {
                    Task = task,
                    Score = _priorityToScore[task.Priority],
                    Key = task.Id
                };
            }

            LogEntries();

            await Task.Delay(_nextDelay);
        }

        void LogEntries()
        {
            var sb = new StringBuilder();
            sb.Append($"[TaskBalancer] Currently scheduled tasks ({_scheduled.Count}):\n");

            foreach (var (_, entry) in _scheduled)
                sb.AppendLine($"    {entry.Task.Id} with score {entry.Score}");

            _logger.LogTrace(sb.ToString());
        }
    }

    private async Task ExecuteLoop(IReadOnlyLifetime lifetime)
    {
        var executionLock = new SemaphoreSlim(_concurrentTasks, _concurrentTasks);

        while (lifetime.IsTerminated == false)
        {
            if (_scheduled.Any() == false)
            {
                await Task.Delay(_emptyDelay);
                continue;
            }

            await executionLock.WaitAsync();

            if (TryPickMaxScored(out var entry) == false)
            {
                executionLock.Release();
                await Task.Delay(_emptyDelay);
                continue;
            }

            Execute(entry).NoAwait();
        }

        return;

        async Task Execute(TaskEntry entry)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogTrace("[TaskBalancer] Executing task, free handles: {count} {taskId}",
                    executionLock.CurrentCount, entry.Task.Id
                );
                
                await entry.Task.Execute();
            }
            catch (Exception e)
            {
                stopwatch.Stop();
                entry.Score -= _exceptionPenalty;
                _scheduled.AddOrUpdate(entry.Key, _ => entry, (_, _) => entry);

                _logger.LogError(e, "[TaskBalancer] Task execution failed in {time} {taskId}",
                    stopwatch.Elapsed, entry.Task.Id
                );
            }
            finally
            {
                executionLock.Release();
            }

            stopwatch.Stop();
            
            _logger.LogTrace("[TaskBalancer] Task execution completed in {time} {taskId}",
                stopwatch.Elapsed, entry.Task.Id
            );
        }
    }

    private bool TryPickMaxScored(out TaskEntry maxEntry)
    {
        maxEntry = null;

        foreach (var (_, entry) in _scheduled)
        {
            if (maxEntry == null || entry.Score > maxEntry.Score)
                maxEntry = entry;
        }

        if (maxEntry == null)
            return false;

        _scheduled.Remove(maxEntry.Key, out _);

        return true;
    }

    public class TaskEntry
    {
        public required string Key { get; init; }
        public required IPriorityTask Task { get; init; }
        public required int Score { get; set; }
    }
}