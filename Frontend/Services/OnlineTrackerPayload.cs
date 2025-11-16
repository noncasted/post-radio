namespace Frontend;

[GenerateSerializer]
public class OnlineTrackerPayload
{
    [Id(0)]
    public required int Value { get; init; }
}