using Internal;

namespace Global.GameLoops
{
    public static class GlobalLoopExtensions
    {
        public static IScopeBuilder AddLoop(this IScopeBuilder builder)
        {
            builder.AddFromFactory<BaseGameLoopFactory>();
            
            return builder;
        }
    }
}