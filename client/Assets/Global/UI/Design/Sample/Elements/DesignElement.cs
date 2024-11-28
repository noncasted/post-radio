using Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Global.UI
{
    [DisallowMultipleComponent]
    public class DesignElement : MonoBehaviour, IDesignElement
    {
        [SerializeField] private DesignElementBehaviour[] _behaviours;
        
        private readonly ViewableProperty<DesignElementState> _state = new(DesignElementState.Idle);
        
        private IReadOnlyLifetime _lifetime;

        public IViewableProperty<DesignElementState> State => _state;

        public IReadOnlyLifetime Lifetime
        {
            get
            {
                if (_lifetime == null || _lifetime.IsTerminated == true)
                    _lifetime = this.GetObjectLifetime();

                return _lifetime;
            }
        }

        public void SetState(DesignElementState state)
        {
            _state.Set(state);
        }

        private void OnEnable()
        { 
            _lifetime = this.GetObjectLifetime();

            foreach (var behaviour in _behaviours)
                behaviour.Construct(this);
        }

        private void OnValidate()
        {
            var behaviours = GetComponentsInChildren<DesignElementBehaviour>(true);

            if (_behaviours == null || _behaviours.Length == 0)
            {
                _behaviours = behaviours;
                return;
            }
            
            if (behaviours.Length < _behaviours.Length)
                return;
            
            _behaviours = behaviours;
        }
        
        [Button("Scan behaviours")]
        private void ScanBehaviours()
        {
            _behaviours = GetComponentsInChildren<DesignElementBehaviour>();
        }
    }
}