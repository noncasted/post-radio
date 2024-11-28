using Internal;

namespace Global.Inputs
{
    public interface IInputView
    {
        IViewableProperty<int> DevicesCount { get; }
        
        IViewableDelegate<IUserInput> UserConnected { get; }
    }
}