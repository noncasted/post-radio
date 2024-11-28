using System;
using NaughtyAttributes;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public class TrajectoryCurve
    {
        [SerializeField] [Min(0f)] private float _time;
        [SerializeField] [CurveRange] private AnimationCurve _move;
        [SerializeField] [CurveRange] private AnimationCurve _height;
        
        public float Time => _time;

        public TrajectoryCurveInstance CreateInstance()
        {
            return new TrajectoryCurveInstance(_move, _height, _time);
        }
    }

    public class TrajectoryCurveInstance
    {
        public TrajectoryCurveInstance(AnimationCurve move, AnimationCurve height, float time)
        {
            _move = move;
            _height = height;
            _time = time;
            _progress = 0f;
        }

        private readonly AnimationCurve _move;
        private readonly AnimationCurve _height;
        private readonly float _time;

        private float _progress;

        public (float, float) Step(float delta)
        {
            _progress += delta / _time;

            if (_progress > 1f)
                _progress = 1f;

            var move = _move.Evaluate(_progress);
            var height = _height.Evaluate(_progress);

            return (move, height);
        }
    }
}