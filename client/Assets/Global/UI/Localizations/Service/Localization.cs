using System;
using Cysharp.Threading.Tasks;
using Global.Publisher;
using Global.Saves;
using Internal;

namespace Global.UI
{
    public class Localization : ILocalization, IDataStorageLoadListener
    {
        public Localization(ILocalizationStorage storage, ISystemLanguageProvider systemLanguageProvider)
        {
            _storage = storage;
            _systemLanguageProvider = systemLanguageProvider;
        }

        private readonly ILocalizationStorage _storage;
        private readonly ISystemLanguageProvider _systemLanguageProvider;

        private Language _language;
        private IDataStorage _dataStorage;

        public Language Language => _language;

        public async UniTask OnDataStorageLoaded(IReadOnlyLifetime lifetime, IDataStorage dataStorage)
        {
            _dataStorage = dataStorage;
            var saves = await _dataStorage.GetEntry<LanguageSave>();

            if (saves.IsOverriden == true)
                _language = saves.Language;
            else
                _language = _systemLanguageProvider.GetLanguage();

            var datas = _storage.GetDatas();

            foreach (var data in datas)
                data.SetLanguage(_language);
        }

        public void Set(Language language)
        {
            _language = language;

            _dataStorage.Save(new LanguageSave()
            {
                IsOverriden = true,
                Language = language
            });

            var datas = _storage.GetDatas();

            foreach (var data in datas)
                data.SetLanguage(_language);
        }

        public Language GetNext(Language language)
        {
            return language switch
            {
                Language.Ru => Language.Eng,
                Language.Eng => Language.Ru,
                _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
            };
        }
    }
}