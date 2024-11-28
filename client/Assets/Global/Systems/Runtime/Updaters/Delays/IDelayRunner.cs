using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Systems
{
    public interface IDelayRunner
    {
        UniTask RunDelay(float time);
        UniTask RunDelay(float time, IReadOnlyLifetime lifetime);
        UniTask RunDelay(float time, Action callback, IReadOnlyLifetime lifetime);
    }
}