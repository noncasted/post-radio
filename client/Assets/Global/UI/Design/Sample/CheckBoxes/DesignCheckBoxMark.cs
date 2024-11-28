using Internal;
using UnityEngine;

namespace Global.UI
{
    [DisallowMultipleComponent]
    public class DesignCheckBoxMark : DesignCheckBoxBehaviour
    {
        [SerializeField] private GameObject _markObject;

        public override void Construct(IDesignCheckBox checkBox, IReadOnlyLifetime lifetime)
        {
            checkBox.IsChecked.View(lifetime, OnStateChanged);
        }

        private void OnStateChanged(bool isChecked)
        {
            _markObject.SetActive(isChecked);
        }
    }
}