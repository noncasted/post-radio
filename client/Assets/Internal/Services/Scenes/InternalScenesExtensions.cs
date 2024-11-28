using VContainer;

namespace Internal
{
    public static class InternalScenesExtensions
    {
        public static IInternalScopeBuilder AddScenes(this IInternalScopeBuilder builder)
        {
            if (builder.Assets.GetOptions<AssetsOptions>().UseAddressables == true)
            {
                builder.Container.Register<AddressablesSceneLoader>(VContainer.Lifetime.Singleton)
                    .As<ISceneLoader>();

                builder.Container.Register<AddressablesScenesUnloader>(VContainer.Lifetime.Singleton)
                    .As<ISceneUnloader>();
            }
            else
            {
                builder.Container.Register<NativeSceneLoader>(VContainer.Lifetime.Singleton)
                    .As<ISceneLoader>();

                builder.Container.Register<NativeSceneUnloader>(VContainer.Lifetime.Singleton)
                    .As<ISceneUnloader>();
            }

            return builder;
        }
    }
}