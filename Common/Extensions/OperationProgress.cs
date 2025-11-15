namespace Common;

public interface IOperationProgress
{
    DateTime StartTime { get; }
    DateTime EndTime { get; }

    IViewableProperty<float> Progress { get; }
    IViewableProperty<OperationStatus> Status { get; }
    IViewableProperty<string> Message { get; }

    void SetProgress(float progress);
    void Log(string message);
    void SetStatus(OperationStatus status);
}

public enum OperationStatus
{
    NotStarted = 0,
    Preparing = 10,
    InProgress = 20,
    Success = 30,
    Failed = 40
}

public class OperationProgress : IOperationProgress
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    private readonly ViewableProperty<float> _progress = new(0);
    private readonly ViewableProperty<OperationStatus> _status = new(OperationStatus.NotStarted);
    private readonly ViewableProperty<string> _message = new(string.Empty);

    private DateTime _startTime;
    private DateTime _endTime;

    public DateTime StartTime => _startTime;
    public DateTime EndTime => _endTime;

    public IViewableProperty<float> Progress => _progress;
    public IViewableProperty<OperationStatus> Status => _status;
    public IViewableProperty<string> Message => _message;

    public void SetProgress(float progress)
    {
        _lock.Wait();
        _progress.Set(progress);
        _lock.Release();
    }

    public void Log(string message)
    {
        _lock.Wait();
        _message.Set(message);
        _lock.Release();
    }

    public void SetStatus(OperationStatus status)
    {
        if (status == OperationStatus.Preparing)
            _startTime = DateTime.UtcNow;
        
        if (status == OperationStatus.Success || status == OperationStatus.Failed)
            _endTime = DateTime.UtcNow;

        _lock.Wait();
        _status.Set(status);
        _lock.Release();
    }
}