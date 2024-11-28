using System;
using Cysharp.Threading.Tasks;

namespace Internal
{
    public static class ViewableDelegatesExtensions
    {
        public static UniTask WaitInvoke(this IViewableDelegate viewableDelegate, IReadOnlyLifetime lifetime)
        {
            var completion = new UniTaskCompletionSource();

            lifetime.Listen(() => { completion.TrySetException(new OperationCanceledException()); });
            viewableDelegate.Advise(lifetime, () => { completion.TrySetResult(); });

            return completion.Task;
        }

        public static UniTask<T> WaitInvoke<T>(
            this IViewableDelegate<T> viewableDelegate,
            IReadOnlyLifetime lifetime)
        {
            var completion = new UniTaskCompletionSource<T>();

            lifetime.Listen(() => completion.TrySetException(new OperationCanceledException()));
            viewableDelegate.Advise(lifetime, value => { completion.TrySetResult(value); });

            return completion.Task;
        }
    }
}