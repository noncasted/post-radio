using System.Text;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Execution;

public interface ITaskQueue
{
    void Enqueue(IPriorityTask task);
    IReadOnlyList<IPriorityTask> Collect();
}

public class TaskQueue : ITaskQueue
{
    public TaskQueue(ILogger<TaskQueue> logger)
    {
        _logger = logger;
    }

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<TaskQueue> _logger;
    private readonly Dictionary<string, Entry> _queue = new();

    public void Enqueue(IPriorityTask task)
    {
        try
        {
            _lock.Wait();

            try
            {
                _queue.TryAdd(task.Id, new Entry
                {
                    Task = task,
                    ScheduleDate = DateTime.UtcNow + task.Delay
                });
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[TaskQueue] Failed to enqueue task {TaskId}", task.Id);
        }
    }

    public IReadOnlyList<IPriorityTask> Collect()
    {
        try
        {
            _lock.Wait();

            try
            {
                var tasks = new List<IPriorityTask>(_queue.Count);

                var sb = new StringBuilder();
                sb.AppendLine($"[TaskQueue] Collecting tasks ({_queue.Count}): ");

                var now = DateTime.UtcNow;

                foreach (var (_, entry) in _queue)
                {
                    if (now < entry.ScheduleDate)
                    {
                        sb.AppendLine(
                            $"    Skipping task {entry.Task.Id}, wait for {(entry.ScheduleDate - now).TotalSeconds:F1}s");

                        continue;
                    }

                    tasks.Add(entry.Task);
                    sb.AppendLine($"    Scheduling task {entry.Task.Id}");
                }

                foreach (var task in tasks)
                    _queue.Remove(task.Id);

                _logger.LogTrace(sb.ToString());

                return tasks;
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[TaskQueue] Failed to collect tasks");
            return Array.Empty<IPriorityTask>();
        }
    }

    private class Entry
    {
        public required IPriorityTask Task { get; init; }
        public required DateTime ScheduleDate { get; init; }
    }
}