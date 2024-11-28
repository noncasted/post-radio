namespace Internal
{
    public static class MathUtils
    {
        public static int ClampPositive(this int value)
        {
            return value < 0 ? 0 : value;
        }
    }
}