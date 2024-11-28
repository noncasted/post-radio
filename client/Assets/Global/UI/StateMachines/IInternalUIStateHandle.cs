namespace Global.UI
{
    public interface IInternalUIStateHandle : IUIStateHandle
    {
        void OnStacked(IInternalUIStateHandle stackHead);
        void ClearStack();
    }
}