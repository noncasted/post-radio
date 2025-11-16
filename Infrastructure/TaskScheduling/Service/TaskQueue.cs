using System.Text;
using Microsoft.Extensions.Logging;

namespace Infrastructure.TaskScheduling;

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
        _lock.Wait();

        if (_queue.ContainsKey(task.Id) == false)
            _queue[task.Id] = new Entry
            {
                Task = task,
                ScheduleDate = DateTime.UtcNow + task.Delay
            };

        _lock.Release();
    }

    public IReadOnlyList<IPriorityTask> Collect()
    {
        if (_queue.Count == 0)
            return [];

        var tasks = new List<IPriorityTask>(_queue.Count);

        _lock.Wait();

        var sb = new StringBuilder();
        sb.AppendLine($"[TaskQueue] Collecting tasks ({_queue.Count}): ");

        foreach (var (_, entry) in _queue)
        {
            if (DateTime.UtcNow < entry.ScheduleDate)
            {
                sb.AppendLine(
                    $"    Skipping task {entry.Task.Id}, wait for {(entry.ScheduleDate - DateTime.UtcNow).TotalSeconds:F1}s"
                );

                continue;
            }

            tasks.Add(entry.Task);
            sb.AppendLine($"    Scheduling task {entry.Task.Id}");
        }

        foreach (var task in tasks)
            _queue.Remove(task.Id);

        _lock.Release();

        _logger.LogTrace(sb.ToString());

        return tasks;
    }

    public class Entry
    {
        public required IPriorityTask Task { get; init; }
        public required DateTime ScheduleDate { get; init; }
    }
}