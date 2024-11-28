using Internal;

namespace Global.Backend
{
    public static class GlobalBackendExtensions
    {
        public static IScopeBuilder AddBackend(this IScopeBuilder builder)
        {
            builder.Register<BackendClient>()
                .As<IBackendClient>();

            builder.Register<TransactionRunner>()
                .As<ITransactionRunner>();

            return builder;
        }
    }
}