using UnityEngine;

namespace Internal
{
    [DisallowMultipleComponent]
    public class GameObjectLifetime : MonoBehaviour, IGameObjectLifetime
    {
        private bool _startInvoked;
        private ILifetime _lifetime;

        private void Start()
        {
            _startInvoked = true;
        }

        private void OnDisable()
        {
            if (_lifetime is { IsTerminated: false })
                _lifetime.Terminate();
        }

        public IReadOnlyLifetime GetValidLifetime()
        {
            if (gameObject.activeInHierarchy == false && _startInvoked == false)
            {
                if (_lifetime == null)
                    _lifetime = new TerminatedLifetime();

                return _lifetime;
            }

            if (_lifetime is { IsTerminated: false })
                return _lifetime;

            _lifetime = new Lifetime();

            return _lifetime;
        }
    }
}