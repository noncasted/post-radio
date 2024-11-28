using Internal;

namespace Global.Systems
{
    public static class GlobalSystemExtensions
    {
        public static IScopeBuilder AddSystemUtils(this IScopeBuilder builder)
        {
            builder.Register<ApplicationProxy>()
                .As<IScreen>()
                .As<IApplicationFlow>();

            var broker = new MessageBroker();
            Msg.Inject(broker);

            builder.RegisterInstance(broker)
                .As<IMessageBroker>();

            builder.Register<ScopeDisposer>()
                .As<IScopeDisposer>();

            var updaterPrefab = builder.GetAsset<GlobalSystemUtilsOptions>().UpdaterPrefab;
            var updater = builder.Instantiate(updaterPrefab);

            builder.RegisterComponent(updater)
                .As<IUpdater>()
                .AsSelfResolvable();

            builder.Register<DelayRunner>()
                .As<IDelayRunner>();

            return builder;
        }
    }
}