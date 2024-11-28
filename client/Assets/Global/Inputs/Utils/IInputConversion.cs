using UnityEngine;

namespace Global.Inputs
{
    public interface IInputConversion
    {
        Vector2 ScreenToWorld(Vector2 position);
    }
}