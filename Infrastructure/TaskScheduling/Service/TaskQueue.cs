namespace Infrastructure.TaskScheduling;

public interface ITaskQueue
{
    void Enqueue(IPriorityTask task);
    IReadOnlyList<IPriorityTask> Collect();
}

public class TaskQueue : ITaskQueue
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, Entry> _queue = new();

    public void Enqueue(IPriorityTask task)
    {
        _lock.Wait();

        _queue[task.Id] = new Entry()
        {
            Task = task,
            ScheduleDate = DateTime.UtcNow + task.Delay,
        };

        _lock.Release();
    }

    public IReadOnlyList<IPriorityTask> Collect()
    {
        var tasks = new List<IPriorityTask>(_queue.Count);

        _lock.Wait();

        foreach (var (_, entry) in _queue)
        {
            if (DateTime.UtcNow < entry.ScheduleDate)
                continue;

            tasks.Add(entry.Task);
        }

        foreach (var task in tasks)
            _queue.Remove(task.Id);

        _lock.Release();

        return tasks;
    }

    public class Entry
    {
        public required IPriorityTask Task { get; init; }
        public required DateTime ScheduleDate { get; init; }
    }
}