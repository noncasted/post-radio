namespace Infrastructure.TaskScheduling;

public interface ITaskScheduler
{
    void Schedule(IPriorityTask task);
}

public class TaskScheduler : ITaskScheduler
{
    public TaskScheduler(ITaskQueue queue)
    {
        _queue = queue;
    }

    private readonly ITaskQueue _queue;
    
    public void Schedule(IPriorityTask task)
    {
        _queue.Enqueue(task);
    }
}