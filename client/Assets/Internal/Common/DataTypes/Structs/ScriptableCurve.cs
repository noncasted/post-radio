using NaughtyAttributes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Internal
{
    [InlineEditor]
    public class ScriptableCurve : ScriptableObject, ICurve
    {
        [SerializeField] [Min(0f)] private float _time;
        [SerializeField] [CurveRange] private AnimationCurve _curve;

        public float Time => _time;
        public AnimationCurve Animation => _curve;
        
        public CurveInstance CreateInstance()
        {
            return new CurveInstance(this);    
        }
    }
}