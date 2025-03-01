using Audio;
using Images;
using SoundCloudExplode;

namespace Core;

public class CoreStartup : IHostedService
{
    public CoreStartup(
        SoundCloudClient soundCloud,
        ISongsRepository songsRepository,
        IImageRepository imageRepository,
        IAudioPreloader audioPreloader)
    {
        _soundCloud = soundCloud;
        _songsRepository = songsRepository;
        _imageRepository = imageRepository;
        _audioPreloader = audioPreloader;
    }

    private readonly SoundCloudClient _soundCloud;
    private readonly ISongsRepository _songsRepository;
    private readonly IImageRepository _imageRepository;
    private readonly IAudioPreloader _audioPreloader;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _soundCloud.InitializeAsync(cancellationToken);
        await _songsRepository.Refresh();
        await _imageRepository.Refresh();
        
        await _audioPreloader.Execute();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}