using Common;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Meta.Images;

public interface IImagesCollection
{
    int Count { get; }
    IReadOnlyList<MediaImage> Images { get; }

    Task Refresh();
    Task<string> GetUrl(int index);
    Task<MediaImage> Save(string fileName, Stream stream);
    Task<bool> Delete(string key);
}

public class ImagesRefreshQueueId : IDurableQueueId
{
    public string ToRaw() => "images-collection-refresh";
}

[GenerateSerializer]
public class ImagesRefreshPayload
{
    [Id(0)] public DateTime RequestedAt { get; init; }
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

    private IReadOnlyList<MediaImage> _entries = new List<MediaImage>();

    public int Count => _entries.Count;
    public IReadOnlyList<MediaImage> Images => _entries;

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        await _mediaStorage.EnsureStorage();
        await _messaging.ListenDurableQueue<ImagesRefreshPayload>(lifetime, _refreshQueue, _ => OnRefreshRequested().NoAwait());
        await OnRefreshRequested();
    }

    public async Task Refresh()
    {
        await OnRefreshRequested();
        await _messaging.PushDirectQueue(_refreshQueue, new ImagesRefreshPayload { RequestedAt = DateTime.UtcNow });
    }

    public Task<string> GetUrl(int index)
    {
        var key = _entries[index].Key;
        return Task.FromResult(_mediaStorage.GetImageUrl(key));
    }

    public async Task<MediaImage> Save(string fileName, Stream stream)
    {
        var image = await _mediaStorage.SaveImage(fileName, stream);
        await Refresh();
        return image;
    }

    public async Task<bool> Delete(string key)
    {
        var removed = await _mediaStorage.DeleteImage(key);
        if (removed)
            await Refresh();

        return removed;
    }

    private async Task OnRefreshRequested()
    {
        _entries = await _mediaStorage.GetImages();
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
