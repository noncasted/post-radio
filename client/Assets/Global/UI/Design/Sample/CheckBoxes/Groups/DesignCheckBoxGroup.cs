using UnityEngine;

namespace Global.UI
{
    [DisallowMultipleComponent]
    public class DesignCheckBoxGroup : MonoBehaviour, IDesignCheckBoxGroupInterceptor
    {
        [SerializeField] private DesignCheckBoxGroupGroupEntry[] _entries;

        private IDesignCheckBoxGroupEntry _current;

        private void Awake()
        {
            foreach (var entry in _entries)
                entry.Construct(this);
        }

        public void OnSelected(IDesignCheckBoxGroupEntry groupEntry)
        {
            if (_current != null)
                _current.Deselect();

            _current = groupEntry;
            groupEntry.Select();
        }
    }
}