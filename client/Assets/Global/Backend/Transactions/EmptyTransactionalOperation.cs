using System;
using Cysharp.Threading.Tasks;
using Global.Systems;
using Internal;

namespace Global.Backend
{
    public class EmptyTransactionalOperation : ResultTransactionalOperation<object>
    {
        public EmptyTransactionalOperation(
            IDelayRunner delayRunner,
            Func<bool, IReadOnlyLifetime, UniTask> action,
            float timeout = 5f, float retryDelay = 0.5f) : base(
            delayRunner,
            async (isRetry, cancellation) =>
            {
                await action.Invoke(isRetry, cancellation);
                return new object();
            },
            timeout, retryDelay)
        {
        }
    }
}