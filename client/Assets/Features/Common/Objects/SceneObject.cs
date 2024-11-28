using Internal;
using UnityEngine;

namespace Common.Objects
{
    [DisallowMultipleComponent]
    public class SceneObject : MonoBehaviour
    {
        private void OnEnable()
        {
            var lifetime = this.GetObjectLifetime();
            OnSetup(lifetime);
        }

        protected virtual void OnSetup(IReadOnlyLifetime lifetime) {}
    }
}