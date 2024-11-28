using System;
using Cysharp.Threading.Tasks;
using Global.Systems;
using Internal;

namespace Global.Backend
{
    public class TransactionRunner : ITransactionRunner
    {
        public TransactionRunner(IDelayRunner delayRunner)
        {
            _delayRunner = delayRunner;
        }

        private readonly IDelayRunner _delayRunner;
        
        public UniTask<T> Run<T>(
            Func<bool, IReadOnlyLifetime, UniTask<T>> action,
            float timeout = 15,
            float retryDelay = 0.5f) where T : class
        {
            return new ResultTransactionalOperation<T>(_delayRunner, action, timeout, retryDelay).Run();
        }

        public UniTask Run(
            Func<bool, IReadOnlyLifetime, UniTask> action,
            float timeout = 15f,
            float retryDelay = 0.5f)
        {
            return new EmptyTransactionalOperation(_delayRunner, action, timeout, retryDelay).Run();
        }
    }
}