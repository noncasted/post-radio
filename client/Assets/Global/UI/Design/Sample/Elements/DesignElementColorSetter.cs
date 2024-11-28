using UnityEngine;
using UnityEngine.UI;

namespace Global.UI
{
    [DisallowMultipleComponent]
    public class DesignElementColorSetter : MonoBehaviour
    {
        [SerializeField] private DesignColor _color;
        [SerializeField] private Image _image;

        private void OnValidate()
        {
            if (_image == null)
                _image = GetComponent<Image>();

            _image.color = _color.Color;
        }
    }
}