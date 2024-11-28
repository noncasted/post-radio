using System.Collections.Generic;
using Internal;
using UnityEngine;

namespace Global.UI
{
    [DisallowMultipleComponent]
    public class DesignLayoutElement : BaseDesignLayoutElement
    {
        [SerializeField] private bool _lockHeight = true;
        [SerializeField] private List<BaseDesignLayoutElement> _children = new();

        private readonly ViewableProperty<float> _height = new();

        public override IViewableProperty<float> Height => _height;

        private void OnEnable()
        {
            var lifetime = this.GetObjectLifetime();

            foreach (var child in _children)
                child.Height.View(lifetime, OnChildHeightChanged);
        }

        public override void BindChild(BaseDesignLayoutElement child)
        {
            var lifetime = this.GetObjectLifetime();
            child.Height.View(lifetime, OnChildHeightChanged);

            _children.Add(child);

            OnChildHeightChanged();
        }

        public override void ForceRecalculate()
        {
            _children.Clear();
            _children.AddRange(this.GetComponentInChildOnly<BaseDesignLayoutElement>());

            foreach (var child in _children)
                child.ForceRecalculate();

            OnChildHeightChanged();
        }

        private void OnChildHeightChanged()
        {
            // if (_lockHeight == true)
            // {
            //     _height.Set(_block.Size.Y.Value);
            //     return;
            // }
            //
            // var yOffset = 0f;
            // var ySpacing = 0f;
            //
            // var autoLayout = _block.AutoLayout;
            //
            // if (autoLayout.Axis == Axis.Y)
            // {
            //     yOffset = autoLayout.Offset;
            //     ySpacing = autoLayout.Spacing.Value;
            // }
            // else
            // {
            //     ySpacing = autoLayout.Cross.Spacing.Value;
            // }
            //
            // var childrenHeight = 0f;
            //
            // foreach (var child in _children)
            //     childrenHeight += child.Height.Value;
            //
            // var totalYSpacing = 0f;
            //
            // if (_children.Count > 1)
            //     totalYSpacing = ySpacing * (_children.Count - 1);
            //
            // var padding = _block.Padding;
            // var totalYPadding = padding.Bottom.Value + padding.Top.Value;
            //
            // var totalHeight = childrenHeight + yOffset + totalYSpacing + totalYPadding;
            //
            // _height.Set(totalHeight);
            // _block.Size.Y.Value = totalHeight;
        }

        private void OnValidate()
        {
            // if (_block == null)
            //     _block = GetComponent<UIBlock>();
            //
            // if (_lockHeight == true)
            //     _height.Set(_block.Size.Y.Value);
        }
    }
}