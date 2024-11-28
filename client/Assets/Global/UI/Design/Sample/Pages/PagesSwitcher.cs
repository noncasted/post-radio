using System.Collections.Generic;
using Global.Systems;
using Internal;
using TMPro;
using UnityEngine;
using VContainer;

namespace Global.UI
{
    [DisallowMultipleComponent]
    public class PagesSwitcher : MonoBehaviour, ISceneService
    {
        [SerializeField] private Curve _moveCurve;
        [SerializeField] private DesignButton _leftButton;
        [SerializeField] private DesignButton _rightButton;
        [SerializeField] private Transform _pagesRoot;
        [SerializeField] private TMP_Text _pagesText;

        private int _index = 0;
        private IUpdater _updater;

        [Inject]
        private void Construct(IUpdater updater)
        {
            _updater = updater;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.Inject(this);
        }
        
        public void Setup(IReadOnlyLifetime lifetime, IReadOnlyList<PageEntry> pages)
        {
            var curve = _moveCurve.CreateInstance();
            var targetPosition = transform.position;
            UpdatePages();

            _pagesRoot.position = transform.position;

            _leftButton.ListenClick(lifetime, () => MoveIndex(-1));
            _rightButton.ListenClick(lifetime, () => MoveIndex(1));

            _updater.RunUpdateAction(lifetime, OnUpdate);

            return;

            void MoveIndex(int delta)
            {
                _index += delta;
                UpdatePages();
                SetMove();
            }

            void UpdatePages()
            {
                _leftButton.gameObject.SetActive(true);
                _rightButton.gameObject.SetActive(true);

                if (_index == 0)
                    _leftButton.gameObject.SetActive(false);

                if (_index == pages.Count - 1)
                    _rightButton.gameObject.SetActive(false);

                _pagesText.text = $"{_index + 1}/{pages.Count}";
            }

            void SetMove()
            {
                curve = _moveCurve.CreateInstance();

                var move = transform.position - pages[_index].transform.position;
                targetPosition = _pagesRoot.position + move;
            }

            void OnUpdate(float delta)
            {
                var evaluation = curve.Step(delta);
                _pagesRoot.position = Vector3.Lerp(_pagesRoot.position, targetPosition, evaluation);
            }
        }
    }
}