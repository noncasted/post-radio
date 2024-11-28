using Internal;
using UnityEngine;
using VContainer;

namespace Global.Cameras
{
    [DisallowMultipleComponent]
    public class CanvasCameraSetter : MonoBehaviour, IScopeSetup, ISceneService
    {
        [SerializeField] private Canvas _canvas;

        private ICurrentCameraProvider _cameraProvider;

        [Inject]
        private void Inject(ICurrentCameraProvider cameraProvider)
        {
            _cameraProvider = cameraProvider;
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _canvas.worldCamera = _cameraProvider.Current;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .AsEventListener<IScopeSetup>();
        }
    }
}