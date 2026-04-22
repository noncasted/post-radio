using Common;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Meta.Images;

public interface IImagesCollection
{
    int Count { get; }

    Task Refresh();
    Task<string> GetUrl(int index);
}

public class ImagesRefreshQueueId : IDurableQueueId
{
    public string ToRaw() => "images-collection-refresh";
}

public class ImagesCollection : IImagesCollection, ICoordinatorSetupCompleted
{
    public ImagesCollection(IMediaStorage mediaStorage, IMessaging messaging)
    {
        _mediaStorage = mediaStorage;
        _messaging = messaging;
    }

    private readonly IMessaging _messaging;
    private readonly IMediaStorage _mediaStorage;
    private readonly ImagesRefreshQueueId _refreshQueue = new();

    private IReadOnlyList<string> _entries = new List<string>();
    public int Count => _entries.Count;

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        await _mediaStorage.EnsureStorage();
        _ = _messaging.ListenDurableQueue<int>(lifetime, _refreshQueue, _ => OnRefreshRequested().NoAwait());
        await OnRefreshRequested();
    }

    public Task Refresh()
    {
        return _messaging.PushDirectQueue(_refreshQueue, 0);
    }

    public Task<string> GetUrl(int index)
    {
        var key = _entries[index];
        return Task.FromResult(_mediaStorage.GetImageUrl(key));
    }

    private async Task OnRefreshRequested()
    {
        _entries = await _mediaStorage.GetImageKeys();
    }
}

public static class ImagesServicesExtensions
{
    public static IHostApplicationBuilder AddImagesServices(this IHostApplicationBuilder builder)
    {
        builder.Add<ImagesCollection>()
               .As<IImagesCollection>()
               .As<ICoordinatorSetupCompleted>();

        return builder;
    }
}