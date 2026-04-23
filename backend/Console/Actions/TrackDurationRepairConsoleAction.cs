using Common.Extensions;
using Meta.Audio;

namespace Console.Actions;

public class TrackDurationRepairConsoleAction : IConsoleAction
{
    public TrackDurationRepairConsoleAction(ITrackDurationRepairService service)
    {
        _service = service;
    }

    private readonly ITrackDurationRepairService _service;

    public string Id => "track-duration-repair";
    public string Name => "Check and repair track durations";
    public string Description => "Compare loaded MP3 duration with SoundCloud duration and redownload mismatched tracks.";

    public Task Execute(IOperationProgress progress, CancellationToken cancellationToken = default)
    {
        return _service.Run(progress, cancellationToken);
    }
}
