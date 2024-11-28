using UnityEngine;
using VContainer.Unity;

namespace Internal
{
    public class ViewInjector : IViewInjector
    {
        public ViewInjector(LifetimeScope scope)
        {
            _scope = scope;
        }

        private readonly LifetimeScope _scope;

        public void Inject<T>(T target) where T : MonoBehaviour
        {
            _scope.Container.Inject(target);
        }

        public void Inject(GameObject target)
        {
            _scope.Container.InjectGameObject(target);
        }
    }
}