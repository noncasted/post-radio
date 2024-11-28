using Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Global.UI
{
    [DisallowMultipleComponent]
    public class DesignCheckBox : DesignElementBehaviour, IDesignCheckBox
    {
        [SerializeField] private DesignCheckBoxBehaviour[] _behaviours;

        private readonly ViewableProperty<bool> _isIsChecked = new();
        private IDesignButton _button;

        public IViewableProperty<bool> IsChecked => _isIsChecked;

        public override void Construct(IDesignElement root)
        {
            // _button = button;
            // button.Clicked.Listen(lifetime, OnClicked);
            //
            // foreach (var behaviour in _behaviours)
            //     behaviour.Construct(this, lifetime);
        }
        
        public void Lock()
        {
            _button.Lock();
        }

        public void Unlock()
        {
            _button.Unlock();
        }

        public void Uncheck()
        {
            _isIsChecked.Set(false);
        }

        private void OnClicked()
        {
            _isIsChecked.Set(!_isIsChecked.Value);
        }

        private void OnValidate()
        {
            if (_behaviours == null || _behaviours.Length == 0)
                _behaviours = GetComponentsInChildren<DesignCheckBoxBehaviour>();
        }

        [Button("Scan behaviours")]
        private void ScanBehaviours()
        {
            _behaviours = GetComponentsInChildren<DesignCheckBoxBehaviour>();
        }
    }
}