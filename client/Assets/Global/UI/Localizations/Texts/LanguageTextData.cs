using System;
using Global.Publisher;
using Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Global.UI
{
    [InlineEditor]
    [CreateAssetMenu(fileName = "LanguageText", menuName = "UI/Localization/Text")]
    public class LanguageTextData : LocalizationEntry
    {
        [SerializeField] [ChildSOField] [Indent]
        private LanguageEntry _eng;

        [SerializeField] [ChildSOField] [Indent]
        private LanguageEntry _ru;

        private readonly ViewableProperty<string> _text = new();

        public override IViewableProperty<string> Text => _text;

        public override void Construct(IAssetEnvironment assets)
        {
            SetLanguage(Language.Eng);
        }

        public override void SetLanguage(Language language)
        {
            try
            {
                var text = language switch
                {
                    Language.Ru => _ru.Text,
                    Language.Eng => _eng.Text,
                    _ => throw new ArgumentOutOfRangeException()
                };

                _text.Set(text);
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception: {e} in text data: {name}");
            }
        }
    }
}