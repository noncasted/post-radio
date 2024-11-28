using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace GamePlay.Audio
{
    public interface IAudioPlayer
    {
        UniTask Play(AudioClip clip, IReadOnlyLifetime lifetime);
    }
}