using Internal;

namespace Global.UI
{
    public static class DesignTextBindExtensions
    {
        public static void Construct(
            this DesignTextBind bind,
            IReadOnlyLifetime lifetime,
            IViewableProperty<string> property)
        {
            bind.Construct(lifetime, property, value => value);
        }

        public static void Construct(
            this DesignTextBind bind,
            IReadOnlyLifetime lifetime,
            IViewableProperty<int> property)
        {
            bind.Construct(lifetime, property, value => value.ToString());
        }
    }
}