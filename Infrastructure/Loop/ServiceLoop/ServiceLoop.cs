using Common;

namespace Infrastructure.Loop;

public interface IServiceLoop
{
    Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime);
    Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime);
}

public class ServiceLoop : IServiceLoop
{
    public ServiceLoop(
        IEnumerable<ILocalSetupCompleted> local,
        IEnumerable<ICoordinatorSetupCompleted> coordinator)
    {
        _local = local;
        _coordinator = coordinator;
    }

    private readonly IEnumerable<ICoordinatorSetupCompleted> _coordinator;

    private readonly IEnumerable<ILocalSetupCompleted> _local;

    public Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        return RunStage(_local, listener => listener.OnLocalSetupCompleted(lifetime));
    }

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        return RunStage(_coordinator, listener => listener.OnCoordinatorSetupCompleted(lifetime));
    }

    private Task RunStage<T>(IEnumerable<T> entries, Func<T, Task> action)
    {
        return Task.WhenAll(entries.Select(action));
    }
}