using UnityEngine;

namespace Global.Cameras
{
    public class CurrentCameraProvider : ICurrentCameraProvider
    {
        private Camera _current;

        public Camera Current => _current;

        public void SetCamera(Camera current)
        {
            _current = current;
        }
    }
}