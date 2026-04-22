using Microsoft.Extensions.Logging;

namespace Infrastructure.Execution;

public interface ITaskScheduler
{
    void Schedule(IPriorityTask task);
}

public class TaskScheduler : ITaskScheduler
{
    public TaskScheduler(ITaskQueue queue, ILogger<TaskScheduler> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    private readonly ITaskQueue _queue;
    private readonly ILogger<TaskScheduler> _logger;

    public void Schedule(IPriorityTask task)
    {
        try
        {
            _queue.Enqueue(task);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[TaskScheduler] Failed to schedule task {TaskId}", task.Id);
        }
    }
}