using Sirenix.OdinInspector;
using UnityEngine;

namespace Global.UI
{
    [InlineEditor]
    [CreateAssetMenu(fileName = "DefaultElementConfig", menuName = "UI/Design/ElementConfig/Default")]
    public class DefaultElementConfig : BaseElementConfig
    {
        [SerializeField] private Color _idle;
        [SerializeField] private Color _hovered;
        [SerializeField] private Color _pressed;
        
        [SerializeField] [Min(0f)] private float _transitionTime;

        public override Color Idle => _idle;
        public override Color Hovered => _hovered;
        public override Color Pressed => _pressed;
        public override float TransitionTime => _transitionTime;
    }
}