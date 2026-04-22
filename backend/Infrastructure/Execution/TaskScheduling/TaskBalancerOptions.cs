using Common;

namespace Infrastructure.Execution;

[GrainState(Table = "configs", State = "task_balancer_config", Lookup = "TaskBalancerConfig",
    Key = GrainKeyType.String)]
public class TaskBalancerOptions
{
    public int EmptyDelayMs { get; set; } = 500;
    public int NextDelayMs { get; set; } = 100;
    public int IterationScore { get; set; } = 1;
    public int ExceptionPenalty { get; set; } = 50;
    public int ConcurrentTasks { get; set; } = 10;
}

public interface ITaskBalancerConfig : IAddressableState<TaskBalancerOptions>
{
}