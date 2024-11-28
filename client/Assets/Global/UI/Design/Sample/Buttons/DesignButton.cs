using Internal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Global.UI
{
    public class DesignButton :
        MonoBehaviour,
        IDesignButton,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] private DesignElement _element;
        [SerializeField] private Button _button;

        private readonly ViewableDelegate _clicked = new();
        private bool _isLocked;
        private DesignElementState _state;

        public DesignElement Element => _element;
        public IViewableDelegate Clicked => _clicked;

        private void OnEnable()
        {
            if (_button == null)
                _button = GetComponent<Button>();

            _element.SetState(DesignElementState.Idle);
            _button.onClick.AddListener(OnClicked);
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(OnClicked);
        }

        public void Lock()
        {
            _isLocked = true;
        }

        public void Unlock()
        {
            _isLocked = false;
            _element.SetState(_state);
        }

        public void OnClicked()
        {
            if (_isLocked == true)
                return;

            _clicked.Invoke();
        }

        private void SetState(DesignElementState state)
        {
            _state = state;

            if (_isLocked == true)
                return;

            _element.SetState(state);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetState(DesignElementState.Hovered);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetState(DesignElementState.Idle);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            SetState(DesignElementState.Pressed);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            SetState(DesignElementState.Hovered);
        }

        private void OnValidate()
        {
            if (_element != null)
                return;

            _element = GetComponent<DesignElement>();

            if (_element == null)
                _element = gameObject.AddComponent<DesignElement>();

            if (_button == null)
                _button = GetComponent<Button>();
        }
    }
}