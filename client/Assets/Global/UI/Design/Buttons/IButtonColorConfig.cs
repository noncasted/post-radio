using UnityEngine;

namespace Global.UI
{
    public interface IButtonColorConfig
    {
        float StateTransitionTime { get; }
        Color Idle { get; }
        Color Hovered { get; }
        Color Pressed { get; }
    }
}