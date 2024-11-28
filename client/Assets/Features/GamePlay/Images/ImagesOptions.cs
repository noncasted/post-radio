using Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GamePlay.Images
{
    [InlineEditor]
    public class ImagesOptions : EnvAsset
    {
        [SerializeField] [Min(0f)] private float _switchDelay;
        [SerializeField] [Min(0f)] private float _transitionTime;
        
        public float SwitchDelay => _switchDelay;
        public float TransitionTime => _transitionTime;
    }
}