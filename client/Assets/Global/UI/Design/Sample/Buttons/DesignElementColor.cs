using System;
using Internal;
using UnityEngine;
using UnityEngine.UI;

namespace Global.UI
{
    [RequireComponent(typeof(Image))]
    public class DesignElementColor : DesignElementBehaviour
    {
        [SerializeField] private BaseElementConfig _config;
        [SerializeField] private Image _image;

        private float _currentTransitionTime;
        private Color _targetColor;
        private Color _fromColor;

        private IDesignElement _element;

        public override void Construct(IDesignElement element)
        {
            _element = element;
            element.State.View(element.Lifetime, OnStateChanged);
        }

        private void Update()
        {
            var progress = _currentTransitionTime / _config.TransitionTime;
            progress = Mathf.Clamp01(progress);

            var color = Color.Lerp(_fromColor, _targetColor, progress);
            _image.color = color;

            _currentTransitionTime += Time.deltaTime;
        }

        public void SetColor(BaseElementConfig config)
        {
            _config = config;
            OnStateChanged(_element.State.Value);
        }

        private void OnStateChanged(DesignElementState state)
        {
            var color = state switch
            {
                DesignElementState.Idle => _config.Idle,
                DesignElementState.Hovered => _config.Hovered,
                DesignElementState.Pressed => _config.Pressed,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            };

            _fromColor = _image.color;
            _targetColor = color;
            _currentTransitionTime = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            GetComponent<Image>().color = _config.Idle;
        }

        private void OnValidate()
        {
            if (_image == null)
                _image = GetComponent<Image>();

            _image.color = _config.Idle;
        }
    }
}