using System;
using Cysharp.Threading.Tasks;
using Global.Publisher.Itch;
using Global.Saves;
using Internal;

namespace Global.Publisher
{
    public static class GlobalPublisherExtensions
    {
        public static async UniTask AddPublisher(this IScopeBuilder builder)
        {
            var platformOptions = builder.GetOptions<PlatformOptions>();

            builder.Register<DataStorageEventLoop>()
                .AsSelf()
                .AsSelfResolvable();

            switch (platformOptions.PlatformType)
            {
                case PlatformType.ItchIO:
                    AddItchIO(builder);
                    break;
                case PlatformType.Yandex:
                    break;
                case PlatformType.IOS:
                    break;
                case PlatformType.Android:
                    break;
                case PlatformType.CrazyGames:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void AddItchIO(IScopeBuilder builder)
        {
            var platformOptions = builder.GetOptions<PlatformOptions>();
            var options = builder.GetAsset<GlobalPublisherOptions>();

            var callbacks = builder.Instantiate(options.ItchCallbacksPrefab);

            builder.Register<ItchAds>()
                .As<IAds>();

            builder.RegisterInstance(callbacks)
                .As<IJsErrorCallback>();

            builder.Register<ItchDataStorage>()
                .WithParameter(SavesExtensions.GetSerializers())
                .As<IDataStorage>()
                .AsEventListener<IScopeBaseSetup>();

            builder.Register<ItchLanguageProvider>()
                .As<ISystemLanguageProvider>();

            if (platformOptions.IsEditor == true)
            {
                builder.Register<ItchLanguageDebugAPI>()
                    .As<IItchLanguageAPI>();
            }
            else
            {
                builder.Register<ItchLanguageExternAPI>()
                    .As<IItchLanguageAPI>();
            }
        }
    }
}