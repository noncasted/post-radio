using Cysharp.Threading.Tasks;
using Global.Inputs;
using Internal;

namespace Global.UI
{
    public class UIStateHandle : IInternalUIStateHandle
    {
        public UIStateHandle(IInternalUIStateHandle parent, IUIState state, IInputConstraintsStorage constraintsStorage)
        {
            _parent = parent;
            State = state;
            Completion = new UniTaskCompletionSource();
            
            _innerLifetime = parent.InnerLifetime.Child();
            _outerLifetime = _innerLifetime.Child();

            _isVisible.View(_innerLifetime, isVisible =>
            {
                if (isVisible == true)
                    constraintsStorage.Add(State.Constraints);
                else
                    constraintsStorage.Remove(State.Constraints);
            });

            _innerLifetime.Listen(() =>
            {
                if (_isVisible.Value == true)
                    constraintsStorage.Remove(State.Constraints);
                
                Completion.TrySetResult();
            });
        }

        private readonly IInternalUIStateHandle _parent;
        private readonly ViewableProperty<bool> _isVisible = new(true);

        private readonly ILifetime _innerLifetime;
        private ILifetime _outerLifetime;

        public IReadOnlyLifetime InnerLifetime => _innerLifetime;
        public IReadOnlyLifetime OuterLifetime => _outerLifetime;
        public IViewableProperty<bool> IsVisible => _isVisible;
        public IUIState State { get; }
        public UniTaskCompletionSource Completion { get; }

        public void OnStacked(IInternalUIStateHandle stackHead)
        {
            _isVisible.Set(false);

            stackHead.InnerLifetime.Listen(() =>
            {
                if (_innerLifetime.IsTerminated == true)
                    return;

                _isVisible.Set(true);
            });
        }

        public void OnChild()
        {
            _parent.IsVisible.View(InnerLifetime, _isVisible.Set);
        }

        public void ClearStack()
        {
            _outerLifetime.Terminate();
            _outerLifetime = _innerLifetime.Child();
        }

        public void Exit()
        {
            _isVisible.Set(false);
            _innerLifetime.Terminate();
        }
    }
}