namespace Infrastructure.Orleans;

public interface ITransactionRunner
{
    public Task Run(TransactionRunOptions options);
}

public class TransactionRunBuilder
{
    public required ITransactionRunner Runner { get; init; }
    public required TransactionRunOptions Options { get; init; }
}

public class TransactionRunOptions
{
    public static readonly TransactionRunOptions Empty = new()
    {
        Action = () => Task.CompletedTask,
        SuccessAction = null,
        FailureAction = null
    };

    public required Func<Task> Action { get; init; }
    public Func<Task>? SuccessAction { get; set; }
    public Func<Task>? FailureAction { get; set; }
}

public static class TransactionRunnerExtensions
{
    public static TransactionRunBuilder Create(this ITransactionRunner runner, Func<Task> action)
    {
        var builder = new TransactionRunBuilder
        {
            Runner = runner,
            Options = new TransactionRunOptions
            {
                Action = action
            }
        };

        return builder;
    }

    extension(TransactionRunBuilder builder)
    {
        public TransactionRunBuilder WithSuccessAction(Func<Task> successAction)
        {
            builder.Options.SuccessAction = successAction;
            return builder;
        }

        public TransactionRunBuilder WithFailureAction(Func<Task> failureAction)
        {
            builder.Options.FailureAction = failureAction;
            return builder;
        }

        public Task Start()
        {
            return builder.Runner.Run(builder.Options);
        }
    }
}