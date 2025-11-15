namespace Common
{
    public class ViewableProperty<T> : LifetimedValue<T>, IViewableProperty<T>
    {
        public ViewableProperty(T value) : base(value)
        {
        }
    }
}