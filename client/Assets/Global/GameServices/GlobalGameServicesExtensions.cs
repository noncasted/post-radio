using Internal;

namespace Global.GameServices
{
    public static class GlobalGameServicesExtensions
    {
        public static IScopeBuilder AddGameServices(this IScopeBuilder builder)
        {
            builder.Register<LocalUsersService>()
                .As<IScopeSetup>();

            builder.Register<LocalUserList>()
                .As<ILocalUserList>()
                .AsSelf();
            
            return builder;
        }
    }
}