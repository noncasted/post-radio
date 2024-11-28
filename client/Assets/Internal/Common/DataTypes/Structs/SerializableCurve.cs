using System;
using NaughtyAttributes;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public class SerializableCurve : ICurve
    {
        [SerializeField] [Min(0f)] private FloatValue _time;
        [SerializeField] [CurveRange] private AnimationCurve _curve;

        public float Time => _time;
        public AnimationCurve Animation => _curve;

        public CurveInstance CreateInstance()
        {
            return new CurveInstance(new Curve(_time, _curve));
        }
    }
}