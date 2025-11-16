namespace Common;

public static class ViewablePropertyExtensions
{
    public static bool IsZero(this IViewableProperty<int> property)
    {
        return property.Value == 0;
    }

    extension(ViewableProperty<int> property)
    {
        public int Increase()
        {
            var value = property.Value + 1;
            property.Set(value);
            return value;
        }

        public int Decrease()
        {
            var value = property.Value - 1;
            property.Set(value);
            return value;
        }

        public bool IsZero()
        {
            return property.Value == 0;
        }

        public void Add(int amount)
        {
            var currentValue = property.Value;
            property.Set(currentValue + amount);
        }

        public void Remove(int amount)
        {
            var currentValue = property.Value;
            property.Set(currentValue - amount);
        }
    }
}