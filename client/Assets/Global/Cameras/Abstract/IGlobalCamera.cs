using UnityEngine;

namespace Global.Cameras
{
    public interface IGlobalCamera
    {
        Camera Camera { get; }
        void Enable();
        void Disable();
    }
}