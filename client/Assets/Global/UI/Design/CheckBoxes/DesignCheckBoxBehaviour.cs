using Internal;
using UnityEngine;

namespace Global.UI
{
    public abstract class DesignCheckBoxBehaviour : MonoBehaviour
    {
        public abstract void Construct(IDesignCheckBox checkBox, IReadOnlyLifetime lifetime);
    }
}