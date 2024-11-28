using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace GamePlay.Audio
{
    [DisallowMultipleComponent]
    public class AudioPlayer : MonoBehaviour, IAudioPlayer, ISceneService
    {
        [SerializeField] private AudioSource _source;

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IAudioPlayer>();
        }
        
        public async UniTask Play(AudioClip clip, IReadOnlyLifetime lifetime)
        {
            _source.clip = null;
            _source.clip = clip;
            _source.time = 0;

            _source.Play();

            var length = clip.length;
            var timer = 0f;

            while (lifetime.IsTerminated == false)
            {
                timer += Time.deltaTime;

                if (_source.isPlaying == true || timer < length)
                    await UniTask.Yield(lifetime.Token);
            }
        }
    }
}