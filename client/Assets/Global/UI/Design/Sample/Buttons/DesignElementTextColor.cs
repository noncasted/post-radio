using System;
using Internal;
using TMPro;
using UnityEngine;

namespace Global.UI
{
    public class DesignElementTextColor : DesignElementBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private BaseElementConfig _config;

        private float _currentTransitionTime;
        private Color _targetColor;
        private Color _fromColor;

        public override void Construct(IDesignElement element)
        {
            element.State.View(element.Lifetime, OnStateChanged);
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

            _fromColor = _text.color;
            _targetColor = color;
            _currentTransitionTime = 0f;
        }
        
        private void Update()
        {
            var progress = _currentTransitionTime / _config.TransitionTime;
            progress = Mathf.Clamp01(progress);

            var color = Color.Lerp(_fromColor, _targetColor, progress);
            _text.color = color;

            _currentTransitionTime += Time.deltaTime;
        }
        
        private void OnValidate()
        {
            if (_text == null)
                _text = GetComponent<TMP_Text>();
            
            _text.color = _config.Idle;
        }
    }
}