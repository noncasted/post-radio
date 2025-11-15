namespace Infrastructure.TaskScheduling;

public enum TaskPriority
{
    Low,
    Medium,
    High,
    Critical
}

public interface IPriorityTask
{
    string Id { get; }
    TaskPriority Priority { get; }

    Task Execute();
}