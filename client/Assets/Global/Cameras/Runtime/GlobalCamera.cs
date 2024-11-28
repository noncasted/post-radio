using Internal;
using UnityEngine;

namespace Global.Cameras
{
    [DisallowMultipleComponent]
    public class GlobalCamera : MonoBehaviour, IGlobalCamera, IScopeBaseSetup
    {
        public Camera Camera { get; private set; }

        public void OnBaseSetup(IReadOnlyLifetime lifetime)
        {
            Camera = GetComponent<Camera>();
        }

        public void Enable()
        {
            gameObject.SetActive(true);
        }

        public void Disable()
        {
            gameObject.SetActive(false);
        }
    }
}