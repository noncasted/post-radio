namespace Common
{
    public static class ViewableDelegatesExtensions
    {
        public static Task WaitInvoke(this IViewableDelegate viewableDelegate, IReadOnlyLifetime lifetime)
        {
            var completion = new TaskCompletionSource();

            lifetime.Listen(() => { completion.TrySetException(new OperationCanceledException()); });
            viewableDelegate.Advise(lifetime, () => { completion.TrySetResult(); });

            return completion.Task;
        }

        public static Task<T> WaitInvoke<T>(
            this IViewableDelegate<T> viewableDelegate,
            IReadOnlyLifetime lifetime)
        {
            var completion = new TaskCompletionSource<T>();

            lifetime.Listen(() => completion.TrySetException(new OperationCanceledException()));
            viewableDelegate.Advise(lifetime, value => { completion.TrySetResult(value); });

            return completion.Task;
        }
    }
}