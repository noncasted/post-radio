using System.Collections.Generic;
using Internal;
using UnityEngine;

namespace Global.UI
{
    [DisallowMultipleComponent]
    public class NavigationTarget : MonoBehaviour, INavigationTarget
    {
        [SerializeField] private RectTransform _rect;
        [SerializeField] private TargetsDictionary _targets;

        [SerializeField] private DesignButton _button;
        [SerializeField] private GameObject _selection;

        private IReadOnlyDictionary<Side, INavigationTarget> _cachedTargets;
        private int _usageCount;
        
        public Vector2 Position => transform.position;

        public IReadOnlyDictionary<Side, INavigationTarget> Targets
        {
            get
            {
                if (_cachedTargets != null)
                    return _cachedTargets;

                var dictionary = new Dictionary<Side, INavigationTarget>();

                foreach (var pair in _targets)
                    dictionary.Add(pair.Key, pair.Value);

                _cachedTargets = dictionary;
                return dictionary;
            }
        }

        public void Setup(TargetsDictionary targets)
        {
            _targets = targets;
        }

        public void Inc()
        {
            _usageCount++;
        }
        
        public void Select()
        {
            _usageCount++;
            _selection.SetActive(true);
        }

        public void Deselect()
        {
            _usageCount--;
            
            if (_usageCount != 0)
                return;
            
            _selection.SetActive(false);
        }

        public void PerformClick()
        {
            _button.OnClicked();
        }

        private void OnValidate()
        {
            if (_rect == null)
                _rect = GetComponent<RectTransform>();

            if (_button == null)
                _button = GetComponent<DesignButton>();
        }
    }
}