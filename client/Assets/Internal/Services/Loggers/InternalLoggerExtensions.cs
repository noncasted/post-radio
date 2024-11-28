using VContainer;

namespace Internal
{
    public static class InternalLoggerExtensions
    {
        public static IInternalScopeBuilder AddLogs(this IInternalScopeBuilder builder)
        {
            builder.Container.Register<Logger>(VContainer.Lifetime.Singleton)
                .As<IGlobalLogger>();

            return builder;
        }
    }
}