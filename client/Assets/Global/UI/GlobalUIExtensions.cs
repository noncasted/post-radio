using Cysharp.Threading.Tasks;
using Global.Publisher;
using Internal;

namespace Global.UI
{
    public static class GlobalUIExtensions
    {
        public static async UniTask AddUI(this IScopeBuilder builder)
        {
            var loadingScreen = await builder.FindOrLoadScene<LoadingScreenScene, LoadingScreen>();
            
            builder.RegisterComponent(loadingScreen)
                .As<ILoadingScreen>();

            builder.Register<LocalizationStorage>()
                .WithAsset<LanguageTextDataRegistry>()
                .As<ILocalizationStorage>();
            
            builder.Register<Localization>()
                .As<ILocalization>()
                .AsEventListener<IDataStorageLoadListener>();

            builder.Register<LanguageConverter>()
                .As<ILanguageConverter>();
            
            builder.Register<UIStateMachine>()
                .As<IUIStateMachine>();
        }
    }
}