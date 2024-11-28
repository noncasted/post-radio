using UnityEngine;

namespace Global.Inputs
{
    public interface ICursor
    {
        Vector2 ScreenPosition { get; }
    }
}