using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Benchmarks;

public class TestCleanup
{
    public TestCleanup(IStateStorage stateStorage, ILogger<TestCleanup> logger)
    {
        _stateStorage = stateStorage;
        _logger = logger;
    }

    private readonly IStateStorage _stateStorage;
    private readonly ILogger<TestCleanup> _logger;
    private readonly List<StateIdentity> _identities = new();

    public void Track<TState>(Guid key) where TState : IStateValue, new()
    {
        var stateInfo = _stateStorage.Registry.Get<TState>();

        _identities.Add(new StateIdentity
        {
            Key = key,
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        });
    }

    public void Track<TState>(string key) where TState : IStateValue, new()
    {
        var stateInfo = _stateStorage.Registry.Get<TState>();

        _identities.Add(new StateIdentity
        {
            Key = key,
            Type = stateInfo.Name,
            TableName = stateInfo.TableName,
            Extension = null
        });
    }

    public void Track(StateIdentity identity)
    {
        _identities.Add(identity);
    }

    public async Task Execute()
    {
        if (_identities.Count == 0)
            return;

        _logger.LogInformation("[TestCleanup] Deleting {Count} state records", _identities.Count);

        await _stateStorage.Delete(_identities);

        _logger.LogInformation("[TestCleanup] Cleanup complete");
        _identities.Clear();
    }
}