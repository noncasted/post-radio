using Common.Reactive;

namespace Infrastructure.Startup;

public interface IClusterParticipantContext
{
    IViewableProperty<bool> IsInitialized { get; }
    IViewableProperty<bool> IsMessagingStarted { get; }
    IReadOnlyList<string> Stages { get; }
    IViewableProperty<int> CurrentStageIndex { get; }

    void SetStages(IReadOnlyList<string> stages);
    void SetStage(string stage);
    void SetMessagingStarted();
    void Initialize();
}

public class ClusterParticipantContext : IClusterParticipantContext
{
    private readonly ViewableProperty<bool> _isInitialized = new(false);
    private readonly ViewableProperty<bool> _isMessagingStarted = new(false);
    private readonly ViewableProperty<int> _currentStageIndex = new(0);

    private List<string> _stages = new();

    public IViewableProperty<bool> IsInitialized => _isInitialized;
    public IViewableProperty<bool> IsMessagingStarted => _isMessagingStarted;
    public IReadOnlyList<string> Stages => _stages;
    public IViewableProperty<int> CurrentStageIndex => _currentStageIndex;

    public void SetStages(IReadOnlyList<string> stages)
    {
        _stages = stages.ToList();
    }

    public void SetStage(string stage)
    {
        var index = _stages.IndexOf(stage);

        if (index >= 0)
        {
            _currentStageIndex.Set(index);
        }
    }

    public void SetMessagingStarted()
    {
        _isMessagingStarted.Set(true);
    }

    public void Initialize()
    {
        _currentStageIndex.Set(_stages.Count);
        _isInitialized.Set(true);
    }
}