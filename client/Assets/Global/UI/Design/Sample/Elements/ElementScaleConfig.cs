using Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Global.UI
{
    [InlineEditor]
    [CreateAssetMenu(fileName = "Scale_", menuName = "UI/Design/ElementConfig/Scale")]
    public class ElementScaleConfig : ScriptableObject
    {
        [SerializeField] private float _hovered;
        [SerializeField] private float _pressed;
        [SerializeField] private Curve _curve;
        
        public float Hovered => _hovered;
        public float Pressed => _pressed;
        public Curve Curve => _curve;
    }
}