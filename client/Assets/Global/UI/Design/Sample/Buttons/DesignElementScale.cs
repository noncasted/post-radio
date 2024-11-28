using System;
using Internal;
using UnityEngine;

namespace Global.UI
{
    [DisallowMultipleComponent]
    public class DesignElementScale : DesignElementBehaviour
    {
        [SerializeField] private ElementScaleConfig _config;

        private CurveInstance _curve;
        private Vector3 _fromScale;
        private Vector3 _targetScale;

        public override void Construct(IDesignElement element)
        {
            element.State.View(element.Lifetime, OnStateChanged);
        }

        private void Update()
        {
            var factor = _curve.Step(Time.deltaTime);

            var scale = Vector3.Lerp(_fromScale, _targetScale, factor);
            transform.localScale = scale;
        }

        private void OnStateChanged(DesignElementState state)
        {
            var scale = state switch
            {
                DesignElementState.Idle => 1f,
                DesignElementState.Hovered => _config.Hovered,
                DesignElementState.Pressed => _config.Pressed,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            };

            _fromScale = transform.localScale;
            _targetScale = Vector3.one * scale;
            _curve = _config.Curve.CreateInstance();
        }
    }
}