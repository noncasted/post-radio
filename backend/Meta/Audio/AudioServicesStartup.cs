using Common;
using Common.Reactive;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoundCloudExplode;

namespace Meta.Audio;

public class AudioServicesStartup : ICoordinatorSetupCompleted
{
    public AudioServicesStartup(
        SoundCloudClient soundCloud,
        IPlaylistsCollection playlists,
        IPlaylistFactory playlistFactory,
        IOptions<AudioOptions> options,
        ILogger<AudioServicesStartup> logger)
    {
        _soundCloud = soundCloud;
        _playlists = playlists;
        _playlistFactory = playlistFactory;
        _options = options.Value;
        _logger = logger;
    }

    private readonly SoundCloudClient _soundCloud;
    private readonly IPlaylistsCollection _playlists;
    private readonly IPlaylistFactory _playlistFactory;
    private readonly AudioOptions _options;
    private readonly ILogger<AudioServicesStartup> _logger;

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        await _soundCloud.InitializeAsync();

        if (string.IsNullOrWhiteSpace(_options.PlaylistsEntryPoint))
        {
            _logger.LogInformation("[Audio] [Startup] PlaylistsEntryPoint is empty — skipping import.");
            return;
        }

        await ImportPlaylists(_options.PlaylistsEntryPoint);
    }

    private async Task ImportPlaylists(string entryPoint)
    {
        _logger.LogInformation("[Audio] Importing playlists from {EntryPoint}", entryPoint);

        var existingUrls = new HashSet<string>(_playlists.Select(kv => kv.Value.Url), StringComparer.OrdinalIgnoreCase);
        var created = 0;
        var skipped = 0;

        try
        {
            await foreach (var playlist in _soundCloud.Users.GetPlaylistsAsync(entryPoint))
            {
                var url = playlist.PermalinkUrl?.ToString();

                if (string.IsNullOrEmpty(url))
                    continue;

                if (existingUrls.Contains(url))
                {
                    skipped++;
                    continue;
                }

                var name = playlist.Title ?? string.Empty;
                await _playlistFactory.Create(url, name);
                existingUrls.Add(url);
                created++;

                _logger.LogInformation("[Audio] Imported playlist {Name} ({Url})", name, url);
            }

            _logger.LogInformation("[Audio] Playlists import complete: {Created} created, {Skipped} skipped", created, skipped);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Audio] Failed to import playlists from {EntryPoint}", entryPoint);
        }
    }
}
