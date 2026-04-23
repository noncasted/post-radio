using System.Net;
using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SoundCloudExplode;

namespace Meta.Audio;

public static class AudioServicesExtensions
{
    private const string SoundCloudHttpClientName = "SoundCloud";

    public static IHostApplicationBuilder AddAudioServices(this IHostApplicationBuilder builder)
    {
        var audioSection = builder.Configuration.GetSection("Audio");
        builder.Services.Configure<AudioOptions>(audioSection);

        builder.AddStateCollection<PlaylistsCollection, Guid, PlaylistState>()
               .As<IPlaylistsCollection>();

        builder.AddStateCollection<SongsCollection, long, SongState>()
               .As<ISongsCollection>();

        builder.Add<PlaylistFactory>()
               .As<IPlaylistFactory>();

        builder.Add<PlaylistLoader>()
               .As<IPlaylistLoader>();

        builder.Add<SongDataLookup>()
               .As<ISongDataLookup>();

        builder.Add<TrackDurationRepairService>()
               .As<ITrackDurationRepairService>();

        builder.Add<AudioServicesStartup>()
               .As<ICoordinatorSetupCompleted>();

        // Named HttpClient reused by PlaylistLoader (for mp3 downloads from *.sndcdn.com)
        // and by SoundCloudClient (for api-v2.soundcloud.com JSON calls).
        // Both go through the same optional SOCKS5 proxy configured via AudioOptions.Socks5Proxy.
        builder.Services.AddHttpClient(SoundCloudHttpClientName)
               .ConfigurePrimaryHttpMessageHandler(CreateSoundCloudHandler);

        builder.Services.AddHttpClient<PlaylistLoader>()
               .ConfigurePrimaryHttpMessageHandler(CreateSoundCloudHandler);

        builder.Services.AddSingleton(serviceProvider =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            return new SoundCloudClient(httpClientFactory.CreateClient(SoundCloudHttpClientName));
        });

        return builder;
    }

    private static HttpMessageHandler CreateSoundCloudHandler(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<AudioOptions>>().Value;

        if (string.IsNullOrWhiteSpace(options.Socks5Proxy))
            return new SocketsHttpHandler();

        return new SocketsHttpHandler
        {
            Proxy = new WebProxy(options.Socks5Proxy),
            UseProxy = true
        };
    }
}
