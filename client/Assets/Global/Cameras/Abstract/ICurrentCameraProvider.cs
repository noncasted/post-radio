using UnityEngine;

namespace Global.Cameras
{
    public interface ICurrentCameraProvider
    {
        Camera Current { get; }

        void SetCamera(Camera current);
    }
}