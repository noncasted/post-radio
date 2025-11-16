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
    private const int _exceptionPenalty = 10;
    private const int _concurrentTasks = 10;

    private readonly ILogger<TaskBalancer> _logger;
    private readonly ITaskQueue _queue;
    private readonly TimeSpan _emptyDelay = TimeSpan.FromMilliseconds(500);
    private readonly TimeSpan _nextDelay = TimeSpan.FromMilliseconds(100);

    private readonly HashSet<string> _queuedTasks = new();
    private readonly SemaphoreSlim _entriesLock = new(1, 1);

    private List<TaskEntry> _entries = new();

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

            await _entriesLock.WaitAsync();

            foreach (var entry in _entries)
                entry.Score += _iterationScore;

            foreach (var task in items)
            {
                if (_queuedTasks.Contains(task.Id) == true)
                    continue;

                var entry = new TaskEntry
                {
                    Task = task,
                    Score = _priorityToScore[task.Priority]
                };

                _queuedTasks.Add(task.Id);
                _entries.Add(entry);
            }

            _entries = _entries.OrderBy(t => t.Score).ToList();

            _entriesLock.Release();

            await Task.Delay(_nextDelay);
        }
    }

    private async Task ExecuteLoop(IReadOnlyLifetime lifetime)
    {
        var executionLock = new SemaphoreSlim(_concurrentTasks, _concurrentTasks);

        while (lifetime.IsTerminated == false)
        {
            if (_entries.Any() == false)
            {
                await Task.Delay(_emptyDelay);
                continue;
            }

            await _entriesLock.WaitAsync();

            var entry = _entries[0];
            _entries.RemoveAt(0);

            _entriesLock.Release();

            Execute(entry).NoAwait();
        }

        return;

        async Task Execute(TaskEntry entry)
        {
            try
            {
                await executionLock.WaitAsync(lifetime.Token);
                await entry.Task.Execute();

                await _entriesLock.WaitAsync();
                _queuedTasks.Remove(entry.Task.Id);
                _entriesLock.Release();
            }
            catch (Exception e)
            {
                await _entriesLock.WaitAsync();

                entry.Score += _exceptionPenalty;
                _entries.Add(entry);

                _entriesLock.Release();
                _logger.LogError(e, "[TaskBalancer] Task execution failed {taskId}", entry.Task.Id);
            }
            finally
            {
                executionLock.Release();
            }
        }
    }

    public class TaskEntry
    {
        public required IPriorityTask Task { get; init; }
        public required int Score { get; set; }
    }
}