using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Systems
{
    public class DelayRunner : IDelayRunner
    {
        public DelayRunner(IUpdater updater)
        {
            _updater = updater;
        }
        
        private readonly IUpdater _updater;

        public UniTask RunDelay(float time)
        {
            var delay = new Delay(_updater, time, null, null);
            return delay.Run();
        }

        public UniTask RunDelay(float time, IReadOnlyLifetime lifetime)
        {
            var delay = new Delay(_updater, time, null, lifetime);
            return delay.Run();
        }

        public UniTask RunDelay(float time, Action callback, IReadOnlyLifetime lifetime)
        {
            var delay = new Delay(_updater, time, callback, lifetime);
            return delay.Run();
        }
    }
}