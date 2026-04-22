using Common.Reactive;

namespace Infrastructure;

public interface IServiceLoop
{
    Task OnOrleansStarted(IReadOnlyLifetime lifetime);
    Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime);
    Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime);
    Task OnServiceStarted(IReadOnlyLifetime lifetime);
}

public class ServiceLoop : IServiceLoop
{
    public ServiceLoop(
        IEnumerable<IOrleansStarted> orleans,
        IEnumerable<ILocalSetupCompleted> local,
        IEnumerable<ICoordinatorSetupCompleted> coordinator,
        IEnumerable<IServiceStarted> started)
    {
        _orleans = orleans;
        _local = local;
        _coordinator = coordinator;
        _started = started;
    }

    private readonly IEnumerable<IOrleansStarted> _orleans;
    private readonly IEnumerable<ILocalSetupCompleted> _local;
    private readonly IEnumerable<ICoordinatorSetupCompleted> _coordinator;
    private readonly IEnumerable<IServiceStarted> _started;

    public Task OnOrleansStarted(IReadOnlyLifetime lifetime)
    {
        return RunStage(_orleans, listener => listener.OnOrleansStarted(lifetime));
    }

    public Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        return RunStage(_local, listener => listener.OnLocalSetupCompleted(lifetime));
    }

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        return RunStage(_coordinator, listener => listener.OnCoordinatorSetupCompleted(lifetime));
    }

    public Task OnServiceStarted(IReadOnlyLifetime lifetime)
    {
        return RunStage(_started, listener => listener.OnServiceStarted(lifetime));
    }

    private Task RunStage<T>(IEnumerable<T> entries, Func<T, Task> action)
    {
        return Task.WhenAll(entries.Select(action));
    }
}