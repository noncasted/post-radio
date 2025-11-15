using SoundCloudExplode;

namespace Frontend.Extensions;

public class CoreStartup : IHostedService
{
    public CoreStartup(
        SoundCloudClient soundCloud,
        IImageRepository imageRepository)
    {
        _soundCloud = soundCloud;
        _imageRepository = imageRepository;
    }

    private readonly SoundCloudClient _soundCloud;
    private readonly IImageRepository _imageRepository;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _imageRepository.Refresh();
        _imageRepository.Run().NoAwait();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}