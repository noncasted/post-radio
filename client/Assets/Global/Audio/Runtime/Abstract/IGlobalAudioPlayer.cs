using UnityEngine;

namespace Global.Audio
{
    public interface IGlobalAudioPlayer
    {
        void PlaySound(AudioClip clip);
        void PlayLoopMusic(AudioClip clip);
    }
}