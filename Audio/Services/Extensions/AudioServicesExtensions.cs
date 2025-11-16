using Common;
using Infrastructure.Loop;
using Microsoft.Extensions.Hosting;

namespace Audio;

public static class AudioServicesExtensions
{
    public static IHostApplicationBuilder AddAudioServices(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.Add<PlaylistFactory>()
            .As<IPlaylistFactory>();

        services.Add<PlaylistLoader>()
            .As<IPlaylistLoader>();
        
        services.Add<PlaylistsCollection>()
            .As<IPlaylistsCollection>()
            .As<ICoordinatorSetupCompleted>();

        services.Add<SongsCollection>()
            .As<ISongsCollection>()
            .As<ICoordinatorSetupCompleted>();

        services.Add<AudioServicesStartup>()
            .As<ICoordinatorSetupCompleted>();
        
        return builder;
    }
}