using UnityEngine;

namespace Global.Cameras
{
    public class CameraUtils : ICameraUtils
    {
        public CameraUtils(ICurrentCameraProvider camera)
        {
            _camera = camera;
        }

        private readonly ICurrentCameraProvider _camera;

        public Vector3 ScreenToWorld(Vector3 screen)
        {
            if (_camera.Current == null)
                return Vector3.zero;

            return _camera.Current.ScreenToWorldPoint(screen);
        }
    }
}