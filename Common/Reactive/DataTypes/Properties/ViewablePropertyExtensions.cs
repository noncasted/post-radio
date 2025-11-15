namespace Common
{
    public static class ViewablePropertyExtensions
    {
        public static int Increase(this ViewableProperty<int> property)
        {
            var value = property.Value + 1;
            property.Set(value);
            return value;
        }
        
        public static int Decrease(this ViewableProperty<int> property)
        {
            var value = property.Value - 1;
            property.Set(value);
            return value;
        }
        
        public static bool IsZero(this ViewableProperty<int> property)
        {
            return property.Value == 0;
        }
        
        public static void Add(this ViewableProperty<int> property, int amount)
        {
            var currentValue = property.Value;
            property.Set(currentValue + amount);
        }
        
        public static void Remove(this ViewableProperty<int> property, int amount)
        {
            var currentValue = property.Value;
            property.Set(currentValue - amount);
        }
        
        public static bool IsZero(this IViewableProperty<int> property)
        {
            return property.Value == 0;
        }
    }
}