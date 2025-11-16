using Common;
using Infrastructure.Loop;
using Infrastructure.Messaging;

namespace Images;

public interface IImagesCollection
{
    int Count { get; }

    Task Refresh();
    Task<string> GetUrl(int index);
}

public class ImagesCollection : IImagesCollection, ICoordinatorSetupCompleted
{
    public ImagesCollection(IObjectStorage objectStorage, IMessaging messaging)
    {
        _objectStorage = objectStorage;
        _messaging = messaging;
    }

    private readonly IMessaging _messaging;
    private readonly IObjectStorage _objectStorage;
    private readonly MessageQueueId _refreshQueue = new("images-collection-refresh");

    private IReadOnlyList<string> _entries = new List<string>();
    public int Count => _entries.Count;

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        _messaging.ListenQueue<int>(lifetime, _refreshQueue, _ => OnRefreshRequested(0).NoAwait());
        return OnRefreshRequested(0);
    }

    public Task Refresh()
    {
        return _messaging.PushDirectQueue(_refreshQueue, 0);
    }

    public Task<string> GetUrl(int index)
    {
        var key = _entries[index];
        return _objectStorage.GetUrl("images", key);
    }

    private async Task OnRefreshRequested(int _)
    {
        _entries = await _objectStorage.GetAllKeys("images");
    }
}