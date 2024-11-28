using Sirenix.OdinInspector;
using UnityEngine;

namespace Global.UI
{
    [InlineEditor]
    [CreateAssetMenu(fileName = "NestedElementConfig", menuName = "UI/Design/ElementConfig/Nested")]
    public class NestedElementConfig : BaseElementConfig
    {
        [SerializeField] private Color _idle;
        [SerializeField] private BaseElementConfig _source;

        public override Color Idle => _idle;
        public override Color Hovered => GetHover();
        public override Color Pressed => GetPressed();
        
        public override float TransitionTime => _source.TransitionTime;

        private Color GetHover()
        {
            var color = _source.Idle - _source.Hovered;
            return _idle - color;
        }
        
        private Color GetPressed()
        {
            var color = _source.Hovered - _source.Pressed;
            return _idle - color;
        }
    }
}