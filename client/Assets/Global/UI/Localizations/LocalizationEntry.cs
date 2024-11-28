using Global.Publisher;
using Internal;

namespace Global.UI
{
    public abstract class LocalizationEntry : EnvAsset
    {
        public abstract void Construct(IAssetEnvironment assets);
        public abstract IViewableProperty<string> Text { get; }

        public abstract void SetLanguage(Language language);
    }
}