using System;
using NaughtyAttributes;
using UnityEngine;

namespace Internal
{
    public interface ICurve
    {
        float Time { get; }
        AnimationCurve Animation { get; }
        
        CurveInstance CreateInstance();
    }

    [Serializable]
    public class Curve : ICurve
    {
        public Curve(float time, AnimationCurve curve)
        {
            _time = time;
            _curve = curve;
        }

        [SerializeField] [Min(0f)] private float _time;
        [SerializeField] [CurveRange] private AnimationCurve _curve;

        public float Time => _time;
        public AnimationCurve Animation => _curve;

        public CurveInstance CreateInstance()
        {
            return new CurveInstance(this);
        }
    }

    public struct CurveInstance
    {
        public CurveInstance(ICurve curve)
        {
            Curve = curve;
            _progress = 0f;
        }


        private float _progress;
        
        public readonly ICurve Curve;
        public bool IsFinished => Mathf.Approximately(_progress, 1f);
        public float Progress => _progress;

        public float Step(float delta)
        {
            _progress += delta / Curve.Time;

            if (_progress > 1f)
                _progress = 1f;

            return Curve.Evaluate(_progress);
        }
    }

    public static class CurveExtensions
    {
        public static float Evaluate(this ICurve curve, float progress)
        {
            return curve.Animation.Evaluate(progress);
        }
    }
}