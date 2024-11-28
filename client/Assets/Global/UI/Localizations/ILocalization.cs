using Global.Publisher;

namespace Global.UI
{
    public interface ILocalization
    {
        Language Language { get; }
        void Set(Language language);
        Language GetNext(Language language);
    }
}