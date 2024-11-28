using UnityEngine;

namespace Global.Systems
{
    public interface IScreen
    {
        ScreenMode ScreenMode { get; }
        Vector2 Resolution { get; }
    }
}