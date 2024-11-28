using Internal;
using UnityEngine;

namespace Global.Audio
{
    public class GlobalAudioOptions : EnvAsset
    {
        [SerializeField] private GlobalGlobalAudioListener _listener;
        [SerializeField] private GlobalAudioPlayer _player;
        
        public GlobalGlobalAudioListener Listener => _listener;
        public GlobalAudioPlayer Player => _player;
    }
}