using System;
using System.Numerics;
using Global.Publisher;

namespace Global.Saves
{
    [Serializable]
    public class WalletSave
    {
        public BigInteger Currency { get; set; }
    }
    
    public class WalletSaveSerializer : StorageEntrySerializer<WalletSave>
    {
        public WalletSaveSerializer() : base("wallet", new WalletSave())
        {
        }
    }
}