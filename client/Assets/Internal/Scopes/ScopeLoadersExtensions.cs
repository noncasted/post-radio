using VContainer;

namespace Internal
{
    public static class ScopeLoadersExtensions
    {
        public static IInternalScopeBuilder AddScopeLoaders(this IInternalScopeBuilder builder)
        {
            builder.Container.Register<ServiceScopeLoader>(VContainer.Lifetime.Singleton)
                .As<IServiceScopeLoader>();

            builder.Container.Register<EntityScopeLoader>(VContainer.Lifetime.Singleton)
                .As<IEntityScopeLoader>();

            return builder;
        }
    }
}