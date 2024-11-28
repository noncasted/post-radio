using System.Collections.Generic;
using Global.Publisher;

namespace Global.Saves
{
    public static class SavesExtensions
    {
        public static IReadOnlyList<IStorageEntrySerializer> GetSerializers()
        {
            return new IStorageEntrySerializer[]
            {
                new AdsSaveSerializer(),
                new LanguageSaveSerializer(),
                new LevelsSaveSerializer(),
                new ScoreSaveSerializer(),
                new TutorialSaveSerializer(),
                new PurchasesSaveSerializer(),
                new UpgradesSaveSerializer(),
                new VolumeSaveSerializer(),
                new WalletSaveSerializer(),
                new EnemiesProgressionSaveSerializer(),
                new PlayerWeaponsSaveSerializer()
            };
        }
    }
}