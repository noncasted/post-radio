using System.Net;
using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

        // Diagnostic: log the resolved Socks5Proxy value at service-registration time so
        // you can tell from the log whether the env var / appsettings actually reached this
        // process. The value is read twice — once eagerly here, and again in
        // CreateSoundCloudHandler when the primary handler is built.
        var earlyProxy = audioSection["Socks5Proxy"];
        Console.WriteLine(earlyProxy is null
            ? "[Audio] [Init] Socks5Proxy: <not set> -> requests will go direct"
            : $"[Audio] [Init] Socks5Proxy resolved from configuration = '{earlyProxy}'");

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
            var logger = serviceProvider.GetRequiredService<ILogger<SoundCloudClient>>();
            var client = httpClientFactory.CreateClient(SoundCloudHttpClientName);
            logger.LogInformation("[Audio] [SoundCloud] SoundCloudClient constructed with HttpClient hash={HttpClientHash}", client.GetHashCode());
            return new SoundCloudClient(client);
        });

        return builder;
    }

    private static HttpMessageHandler CreateSoundCloudHandler(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<AudioOptions>>().Value;
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Meta.Audio.Handler");
        var handler = new SocketsHttpHandler
        {
            // Disable automatic proxy — SOCKS5 is handled manually via ConnectCallback below.
            UseProxy = false
        };

        if (!string.IsNullOrWhiteSpace(options.Socks5Proxy))
        {
            try
            {
                handler.ConnectCallback = Socks5ConnectCallback.Create(options.Socks5Proxy, logger);
                logger.LogInformation("[Audio] [Handler] SOCKS5 proxy ENABLED via manual ConnectCallback: {Proxy}", options.Socks5Proxy);
            }
            catch (Exception e)
            {
                logger.LogError(e, "[Audio] [Handler] Failed to parse Socks5Proxy='{Proxy}' — falling back to direct connection", options.Socks5Proxy);
            }
        }
        else
        {
            logger.LogWarning("[Audio] [Handler] SOCKS5 proxy NOT configured — requests go direct. " +
                              "Set Audio:Socks5Proxy (e.g. 'socks5://127.0.0.1:1080') to route via the VPS tunnel.");
        }

        return handler;
    }
}
