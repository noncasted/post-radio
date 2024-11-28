using Internal;

namespace Global.UI
{
    public interface IDesignCheckBox
    {
        IViewableProperty<bool> IsChecked { get; }

        void Lock();
        void Unlock();

        void Uncheck();
    }
}