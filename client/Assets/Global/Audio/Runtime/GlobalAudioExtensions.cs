using Global.Publisher;
using Internal;

namespace Global.Audio
{
    public static class GlobalAudioExtensions
    {
        public static IScopeBuilder AddAudio(this IScopeBuilder builder)
        {
            var config = builder.GetAsset<GlobalAudioOptions>();
            var player = builder.Instantiate(config.Player);
            var listener = builder.Instantiate(config.Listener);
            
            builder.RegisterComponent(player)
                .As<IGlobalVolume>()
                .As<IGlobalAudioPlayer>()
                .AsEventListener<IDataStorageLoadListener>();

            
            builder.RegisterComponent(listener)
                .As<IGlobalAudioListener>()
                .AsEventListener<IScopeBaseSetup>();

            return builder;
        }
    }
}