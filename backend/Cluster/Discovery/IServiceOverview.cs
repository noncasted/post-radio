namespace Cluster.Discovery;

public interface IServiceOverview
{
    Guid Id { get; }
    ServiceTag Tag { get; }
    DateTime UpdateTime { get; }
}

[GenerateSerializer]
public class ServiceOverview : IServiceOverview
{
    [Id(0)] public required Guid Id { get; init; }
    [Id(1)] public required ServiceTag Tag { get; init; }
    [Id(2)] public required DateTime UpdateTime { get; init; }
}
