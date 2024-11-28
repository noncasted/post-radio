namespace Global.UI
{
    public interface IUIStateMachine
    {
        IUIState Base { get; }
        
        IUIStateHandle CreateChild(IUIState parent, IUIState state);
        IUIStateHandle CreateStackChild(IUIState parent, IUIState state);
        void ClearStack(IUIState state);
        void Exit(IUIState state);
    }
}