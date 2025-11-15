using System.Collections.Concurrent;

namespace Infrastructure.TaskScheduling;

public interface ITaskQueue
{
    void Enqueue(IPriorityTask task);
    IReadOnlyList<IPriorityTask> Collect();
}

public class TaskQueue : ITaskQueue
{
    private readonly ConcurrentBag<IPriorityTask> _queue = new();
    
    public void Enqueue(IPriorityTask task)
    {
        _queue.Add(task);
    }

    public IReadOnlyList<IPriorityTask> Collect()
    {
        var tasks = new List<IPriorityTask>(_queue.Count);

        while (_queue.TryTake(out var task))
            tasks.Add(task);
        
        return tasks;
    }
}