using Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Options;

namespace Audio;

public static class AudioServicesExtensions
{
    public static IHostApplicationBuilder AddAudioServices(this IHostApplicationBuilder builder)
    {
        builder.AddOptionsFile("Settings/appsettings.playlists.json");
        builder.AddOptions<PlaylistsOptions>("PlaylistsOptions");
        
        builder.Services.AddSingleton<ISongProvider, SongProvider>();

        builder.Services.AddSingleton<ISongsRepository, SongsRepository>();
        builder.Services.AddSingleton<IAudioAPI, AudioAPI>();

        return builder;
    }
}