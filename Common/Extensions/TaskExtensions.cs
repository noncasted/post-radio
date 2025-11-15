namespace Common;

public static class TaskExtensions
{
    private static readonly Action<Task> _noAwaitContinuation = (t =>
    {
        if (t.Exception == null || t.Exception.IsOperationCanceled())
            return;

        throw t.Exception;
    });

    public static void NoAwait(this Task? task)
    {
        task?.ContinueWith(_noAwaitContinuation,
            TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);
    }

    public static bool IsOperationCanceled(this Exception? exception)
    {
        switch (exception)
        {
            case null:
                return false;
            case OperationCanceledException _:
                return true;
            case AggregateException aggregateException:
                if (aggregateException.InnerExceptions.Count == 0)
                    return false;
                foreach (Exception innerException in aggregateException.InnerExceptions)
                {
                    if (!innerException.IsOperationCanceled())
                        return false;
                }

                return true;
            default:
                return exception.InnerException.IsOperationCanceled();
        }
    }
}