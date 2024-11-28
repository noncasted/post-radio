using UnityEngine;

namespace Global.UI
{
    public abstract class BaseElementConfig : ScriptableObject
    {
        public abstract Color Idle { get; }
        public abstract Color Hovered { get; }
        public abstract Color Pressed { get; }
        
        public abstract float TransitionTime { get; }
    }
}