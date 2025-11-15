using Common;
using ServiceLoop;
using SoundCloudExplode;

namespace Audio;

public class AudioServicesStartup : ICoordinatorSetupCompleted
{
    public AudioServicesStartup(SoundCloudClient soundCloud)
    {
        _soundCloud = soundCloud;
    }

    private readonly SoundCloudClient _soundCloud;

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        return _soundCloud.InitializeAsync();
    }
}