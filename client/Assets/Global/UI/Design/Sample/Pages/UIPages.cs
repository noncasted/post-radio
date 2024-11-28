using System.Collections.Generic;
using System.Linq;
using Internal;
using UnityEngine;

namespace Global.UI
{
    [DisallowMultipleComponent]
    public class UIPages : MonoBehaviour
    {
        [SerializeField] private GameObject[] _prefabs;
        [SerializeField] private PageEntry _pagePrefab;
        [SerializeField] private int _pageCapacity;
        [SerializeField] private Transform _pagesRoot;
        [SerializeField] private PagesSwitcher _switcher;

        private readonly List<PageEntry> _pages = new();

        public T CreateElement<T>() where T : MonoBehaviour
        {
            var prefab = _prefabs.Random();
            var element = Instantiate(prefab, _pagesRoot).GetComponent<T>();

            if (_pages.Count == 0 || _pages.Last().Entries == _pageCapacity)
            {
                var newPage = Instantiate(_pagePrefab, _pagesRoot);
                _pages.Add(newPage);
            }

            var page = _pages.Last();
            page.AddEntry(element.transform);

            return element;
        }

        public void Setup(IReadOnlyLifetime lifetime)
        {
            _switcher.Setup(lifetime, _pages);
        }
    }
}