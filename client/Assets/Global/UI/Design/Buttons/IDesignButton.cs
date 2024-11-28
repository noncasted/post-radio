using Internal;

namespace Global.UI
{
    public interface IDesignButton
    {
        IViewableDelegate Clicked { get; }

        void Lock();
        void Unlock();
    }
}