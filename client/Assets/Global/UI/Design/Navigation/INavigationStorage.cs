namespace Global.UI
{
    public interface INavigationStorage
    {
        INavigationTarget First { get; }

        void Recalculate();
    }
}