using Common;

namespace ServiceLoop;

public interface IServiceLoopObserver
{
    IViewableProperty<bool> IsOrleansStarted { get; }
}

public class ServiceLoopObserver : 
    ILifecycleParticipant<IClusterClientLifecycle>,
    ILifecycleParticipant<ISiloLifecycle>,
    IServiceLoopObserver
{
    private readonly ViewableProperty<bool> _isOrleansStarted = new(false);

    public IViewableProperty<bool> IsOrleansStarted => _isOrleansStarted;
    
    public void Participate(IClusterClientLifecycle lifecycle)
    {
        _isOrleansStarted.Set(true);
    }

    public void Participate(ISiloLifecycle lifecycle)
    {
        _isOrleansStarted.Set(true);
    }
}