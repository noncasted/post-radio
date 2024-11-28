using Internal;
using UnityEngine;

namespace Global.UI
{
    [DisallowMultipleComponent]
    public class DesignCheckBoxGroupGroupEntry : DesignCheckBoxBehaviour, IDesignCheckBoxGroupEntry
    {
        [SerializeField] private string _key;

        private IDesignCheckBoxGroupInterceptor _interceptor;
        private IDesignCheckBox _checkBox;

        public string Key => _key;
        
        public override void Construct(IDesignCheckBox checkBox, IReadOnlyLifetime lifetime)
        {
            _checkBox = checkBox;
            checkBox.IsChecked.View(lifetime, OnStateChanged);
        }
        
        public void Construct(IDesignCheckBoxGroupInterceptor interceptor)
        {
            _interceptor = interceptor;
        }

        public void Deselect()
        {
            _checkBox.Unlock();
            _checkBox.Uncheck();
        }
        
        public void Select()
        {
            _checkBox.Lock();
        }

        private void OnStateChanged(bool isChecked)
        {
            if (isChecked == false)
                return;
            
            _interceptor.OnSelected(this);
        }
    }
}