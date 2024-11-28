using System;
using Cysharp.Threading.Tasks;
using Global.Systems;
using Internal;
using UnityEngine;

namespace Global.Backend
{
    public class ResultTransactionalOperation<T> where T : class
    {
        public ResultTransactionalOperation(
            IDelayRunner delayRunner,
            Func<bool, IReadOnlyLifetime, UniTask<T>> action,
            float timeout,
            float retryDelay)
        {
            _delayRunner = delayRunner;
            _action = action;
            _timeout = timeout;
            _retryDelay = retryDelay;
        }

        private readonly IDelayRunner _delayRunner;
        private readonly Func<bool, IReadOnlyLifetime, UniTask<T>> _action;
        private readonly float _timeout;
        private readonly float _retryDelay;

        private float _timer;

        public async UniTask<T> Run()
        {
            var isSuccess = false;
            var isRetry = false;
            T result = null;

            while (isSuccess == false)
            {
                var lifetime = new Lifetime();

                if (isRetry == true)
                    Debug.Log("Start transaction retry");

                try
                {
                    _delayRunner.RunDelay(_timeout, OnTimeout, lifetime).Forget();
                    result = await _action.Invoke(isRetry, lifetime);
                    isSuccess = true;
                }
                catch (Exception exception)
                {
                    Debug.Log($"Exception in result transaction: {exception.Message}");

                    lifetime.Terminate();

                    isSuccess = false;
                    isRetry = true;

                    await _delayRunner.RunDelay(_retryDelay);
                }

                if (isRetry == true)
                    Debug.Log("Transaction completed with retry");

                lifetime.Terminate();
            }

            return result;

            void OnTimeout()
            {
                isSuccess = false;
                isRetry = true;
            }
        }
    }
}