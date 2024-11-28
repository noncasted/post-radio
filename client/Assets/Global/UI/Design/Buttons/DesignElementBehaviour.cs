using UnityEngine;

namespace Global.UI
{
    public abstract class DesignElementBehaviour : MonoBehaviour
    {
        public abstract void Construct(IDesignElement root);
    }
}