using Common.Reactive;
using Infrastructure.Execution;

namespace Benchmarks;

public class TestPriorityTask : IPriorityTask
{
    public TestPriorityTask(
        string id,
        TaskPriority priority,
        TimeSpan delay = default,
        List<string>? log = null,
        int failCount = 0,
        Func<Task>? execute = null)
    {
        Id = id;
        Priority = priority;
        Delay = delay;
        _log = log;
        _failCount = failCount;
        _execute = execute;
    }

    private readonly List<string>? _log;
    private readonly Func<Task>? _execute;
    private int _failCount;

    public string Id { get; }
    public TaskPriority Priority { get; }
    public TimeSpan Delay { get; }

    public async Task Execute()
    {
        _log?.Add(Id);

        if (_failCount > 0)
        {
            _failCount--;
            throw new Exception($"TestPriorityTask {Id} intentional failure");
        }

        if (_execute != null)
            await _execute();
    }
}

public class TestBalancerConfig : ITaskBalancerConfig
{
    public TestBalancerConfig(TaskBalancerOptions options)
    {
        Value = options;
    }

    public TaskBalancerOptions Value { get; }
    public bool IsInitialized => true;
    public IReadOnlyLifetime ValueLifetime => throw new NotImplementedException();
    public Task SetValue(TaskBalancerOptions value) => throw new NotImplementedException();

    public void Advise(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, TaskBalancerOptions> handler) =>
        throw new NotImplementedException();

    public void View(IReadOnlyLifetime lifetime, Action<IReadOnlyLifetime, TaskBalancerOptions> handler) =>
        throw new NotImplementedException();

    public void Dispose() => throw new NotImplementedException();
}