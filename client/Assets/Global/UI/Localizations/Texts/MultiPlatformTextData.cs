using System;
using Global.Publisher;
using Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Global.UI
{
    [InlineEditor]
    [CreateAssetMenu(fileName = "PlatformLanguageText", menuName = "UI/Localization/PlatformText")]
    public class MultiPlatformTextData : LocalizationEntry
    {
        [SerializeField] [ChildSOField] [Indent]
        private LanguageEntry _engMobile;

        [SerializeField] [ChildSOField] [Indent]
        private LanguageEntry _engDesktop;

        [SerializeField] [ChildSOField] [Indent]
        private LanguageEntry _ruMobile;

        [SerializeField] [ChildSOField] [Indent]
        private LanguageEntry _ruDesktop;

        private readonly ViewableProperty<string> _text = new();

        private PlatformOptions _platformOptions;

        public override IViewableProperty<string> Text => _text;

        public override void Construct(IAssetEnvironment assets)
        {
            _platformOptions = assets.GetOptions<PlatformOptions>();
        }

        public override void SetLanguage(Language language)
        {
            var entry = GetEntry();
            _text.Set(entry.Text);

            return;

            LanguageEntry GetEntry()
            {
                var isMobile = _platformOptions.IsMobile;

                if (isMobile == true)
                {
                    return language switch
                    {
                        Language.Eng => _engMobile,
                        Language.Ru => _ruMobile,
                        _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
                    };
                }
                else
                {
                    return language switch
                    {
                        Language.Eng => _engDesktop,
                        Language.Ru => _ruDesktop,
                        _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
                    };
                }
            }
        }
    }
}