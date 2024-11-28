using Internal;
using UnityEngine;

namespace Global.Cameras
{
    public static class GlobalCameraExtensions
    {
        public static IScopeBuilder AddCamera(this IScopeBuilder builder)
        {
            builder.Register<CurrentCameraProvider>()
                .As<ICurrentCameraProvider>();

            var prefab = builder.GetAsset<GlobalCameraOptions>().Prefab;
            var camera = builder.Instantiate(prefab, new Vector3(0f, 0f, -10f));
            camera.gameObject.SetActive(false);

            builder.RegisterComponent(camera)
                .As<IGlobalCamera>()
                .AsEventListener<IScopeBaseSetup>();
            
            builder.Register<CameraUtils>()
                .As<ICameraUtils>();

            return builder;
        }
    }
}