namespace Infrastructure.Discovery;

public interface IServiceOverview
{
    public Guid Id { get; }
    public ServiceTag Tag { get; }
    public DateTime UpdateTime { get; }
}

[GenerateSerializer]
public class ServiceOverview : IServiceOverview
{
    [Id(0)] public required Guid Id { get; init; }
    [Id(2)] public required ServiceTag Tag { get; init; }
    [Id(3)] public required DateTime UpdateTime { get; init; }
}