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
    Task<IReadOnlyList<string>> GetUrls(int start, int count);
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

    /// <summary>
    /// A whole slideshow batch in one call. The frontend walks a contiguous run from a random
    /// offset, so <paramref name="start"/> wraps instead of clamping, and asking per index
    /// would cost one frontend-to-gateway round trip per picture.
    /// </summary>
    public Task<IReadOnlyList<string>> GetUrls(int start, int count)
    {
        var entries = _entries;

        if (entries.Count == 0 || count <= 0)
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        var offset = (int)(((long)start % entries.Count + entries.Count) % entries.Count);
        var urls = new List<string>(count);

        for (var i = 0; i < count; i++)
            urls.Add(_mediaStorage.GetImageUrl(entries[(offset + i) % entries.Count].Key));

        return Task.FromResult<IReadOnlyList<string>>(urls);
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
