using UnityEngine;

namespace Global.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class PageEntry : MonoBehaviour
    {
        private int _entries;

        public int Entries => _entries;

        public void AddEntry(Transform entry)
        {
            _entries++;
            entry.SetParent(transform);
        }
    }
}