using System.Collections.Generic;

namespace Global.UI
{
    public interface ILocalizationStorage
    {
        IReadOnlyList<LanguageTextData> GetDatas();
    }
}