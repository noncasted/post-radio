using Cysharp.Threading.Tasks;
using Internal;

namespace Global.UI
{
    public class BaseUIState : IInternalUIStateHandle, IUIState
    {
        public BaseUIState()
        {
            OuterLifetime = new Lifetime();
            InnerLifetime = new Lifetime();
            Recovered = new ViewableDelegate();
        }
        
        public IUIConstraints Constraints => new UIConstraints();

        public IReadOnlyLifetime InnerLifetime { get; }
        public IReadOnlyLifetime OuterLifetime { get; }
        public IViewableProperty<bool> IsVisible { get; } = new ViewableProperty<bool>(true);
        public IViewableDelegate Recovered { get; }
        public IUIState State => this;
        public UniTaskCompletionSource Completion { get; } = new();

        public void Exit()
        {
        }

        public void OnStacked(IInternalUIStateHandle stackHead)
        {
 
        }

        public void ClearStack()
        {
        }
    }
}