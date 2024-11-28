using UnityEngine;

namespace Global.Cameras
{
    public interface ICameraUtils
    {
        Vector3 ScreenToWorld(Vector3 screen);
    }
}