using System.Collections.Generic;

namespace Global.UI
{
    public class LocalizationStorage : ILocalizationStorage
    {
        public LocalizationStorage(LanguageTextDataRegistry registry)
        {
            _registry = registry;
        }

        private readonly LanguageTextDataRegistry _registry;
        
        public IReadOnlyList<LanguageTextData> GetDatas()
        {
            return _registry.Objects;
        }
    }
}