using Internal;
using UnityEngine;

namespace Global.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GlobalGlobalAudioListener))]
    public class GlobalGlobalAudioListener :
        MonoBehaviour,
        IGlobalAudioListener,
        IScopeBaseSetup
    {
        private AudioListener _listener;

        public void OnBaseSetup(IReadOnlyLifetime lifetime)
        {
            _listener = GetComponent<AudioListener>();
            Enable();
            
        }

        public void Enable()
        {
            _listener.enabled = true;
        }

        public void Disable()
        {
            _listener.enabled = false;
        }
    }
}