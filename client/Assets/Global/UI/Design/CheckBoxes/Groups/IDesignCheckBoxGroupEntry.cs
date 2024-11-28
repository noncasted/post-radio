namespace Global.UI
{
    public interface IDesignCheckBoxGroupEntry
    {
        string Key { get; }

        void Select();
        void Deselect();
    }
}