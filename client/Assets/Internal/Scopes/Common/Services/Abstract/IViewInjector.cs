using UnityEngine;

namespace Internal
{
    public interface IViewInjector
    {
        void Inject<T>(T target) where T : MonoBehaviour;
        void Inject(GameObject target);
    }
}