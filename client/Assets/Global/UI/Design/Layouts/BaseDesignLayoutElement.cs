using Internal;
using UnityEngine;

namespace Global.UI
{
    public abstract class BaseDesignLayoutElement : MonoBehaviour
    {
        public abstract IViewableProperty<float> Height { get; }

        public abstract void BindChild(BaseDesignLayoutElement child);
        public abstract void ForceRecalculate();
    }
}