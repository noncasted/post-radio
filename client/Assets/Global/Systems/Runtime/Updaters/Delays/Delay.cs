using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Systems
{
    public class Delay : IDelay, IUpdatable
    {
        public Delay(
            IUpdater updater,
            float delay,
            Action callback,
            IReadOnlyLifetime lifetime)
        {
            _updater = updater;
            _delay = delay;
            _callback = callback;
            _lifetime = lifetime;
            _completion = new UniTaskCompletionSource();
        }

        private readonly IUpdater _updater;
        private readonly float _delay;
        private readonly Action _callback;
        private readonly IReadOnlyLifetime _lifetime;
        private readonly UniTaskCompletionSource _completion;

        private float _timer;
        private bool _wasCanceled;
        
        public async UniTask Run()
        {
            _lifetime.Listen(OnCanceled);
            _updater.Add(_lifetime, this);
            
            await _completion.Task;

            _callback?.Invoke();
        }

        public void OnUpdate(float delta)
        {
            _timer += delta;
            
            if (_timer < _delay)
                return;

            _completion.TrySetResult();
        }

        private void OnCanceled()
        {
            if (_wasCanceled == true)
                return;

            _wasCanceled = true;
        }
    }
}