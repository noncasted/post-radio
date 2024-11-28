using UnityEngine;

namespace Global.UI
{
    [DisallowMultipleComponent]
    public class DesignLayoutRoot : MonoBehaviour
    {
        public void ForceRecalculate()
        {
            var children = this.GetComponentInChildOnlyIncludeSelf<BaseDesignLayoutElement>();

            foreach (var child in children)
                child.ForceRecalculate();
        }

        private void OnDrawGizmos()
        {
            ForceRecalculate();
        }
    }
}