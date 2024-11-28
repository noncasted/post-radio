using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Backend
{
    public interface ITransactionRunner
    {
        public UniTask<T> Run<T>(
            Func<bool, IReadOnlyLifetime, UniTask<T>> action,
            float timeout = 15f,
            float retryDelay = 0.5f) where T : class;

        public UniTask Run(
            Func<bool, IReadOnlyLifetime, UniTask> action,
            float timeout = 15f,
            float retryDelay = 0.5f);
    }
}