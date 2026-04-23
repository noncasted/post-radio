using Common.Extensions;

namespace Console.Actions;

public interface IConsoleAction
{
    string Id { get; }
    string Name { get; }
    string Description { get; }

    Task Execute(IOperationProgress progress, CancellationToken cancellationToken = default);
}
