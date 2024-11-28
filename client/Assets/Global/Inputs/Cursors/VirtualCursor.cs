using Global.Systems;
using Internal;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Global.Inputs
{
    public class VirtualCursor : ICursor, IPreUpdatable, IScopeSetup
    {
        public VirtualCursor(IUpdater updater)
        {
            _updater = updater;
        }

        private readonly IUpdater _updater;
        
        private Vector2 _screenPosition = Vector2.zero;

        public Vector2 ScreenPosition => _screenPosition;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _updater.Add(lifetime, this);
        }

        public void OnPreUpdate(float delta)
        {
            _screenPosition = Mouse.current.position.ReadValue();
        }
    }
}