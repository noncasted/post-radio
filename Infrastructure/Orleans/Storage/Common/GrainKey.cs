using System.Globalization;
using System.Text;

namespace Infrastructure.Orleans;

public class GrainKey
{
    public long Id_0 { get; }

    public long Id_1 { get; }

    public string StringKey { get; }

    public bool IsLongKey { get; }

    public bool IsGuidKey { get; }

    public bool IsStringKey { get; }

    public GrainKey(long key, string keyExtension)
    {
        Id_0 = 0;
        Id_1 = key;
        StringKey = keyExtension;

        IsLongKey = true;
        IsGuidKey = false;
        IsStringKey = false;
    }

    public GrainKey(Guid key, string keyExtension)
    {
        var guidKeyBytes = key.ToByteArray();
        Id_0 = BitConverter.ToInt64(guidKeyBytes, 0);
        Id_1 = BitConverter.ToInt64(guidKeyBytes, 8);
        StringKey = keyExtension;

        IsLongKey = false;
        IsGuidKey = true;
        IsStringKey = false;
    }

    public GrainKey(string key)
    {
        StringKey = key;
        Id_0 = 0;
        Id_1 = 0;

        IsLongKey = false;
        IsGuidKey = false;
        IsStringKey = true;
    }

    public byte[] GetHashBytes()
    {
        byte[] bytes = null;

        if (IsLongKey)
        {
            bytes = BitConverter.GetBytes(Id_1);
        }
        else if (IsGuidKey)
        {
            bytes = ToGuidKey(Id_0, Id_1).ToByteArray();
        }

        if (bytes != null && StringKey != null)
        {
            var oldLen = bytes.Length;
            var stringBytes = Encoding.UTF8.GetBytes(StringKey);
            Array.Resize(ref bytes, bytes.Length + stringBytes.Length);
            Array.Copy(stringBytes, 0, bytes, oldLen, stringBytes.Length);
        }

        if (bytes == null)
            bytes = Encoding.UTF8.GetBytes(StringKey);

        if (BitConverter.IsLittleEndian == true)
            Array.Reverse(bytes);

        return bytes;
    }

    public override string ToString()
    {
        string primaryKey;
        string keyExtension = null;
        
        if (IsLongKey)
        {
            primaryKey = Id_1.ToString(CultureInfo.InvariantCulture);
            keyExtension = StringKey;
        }
        else if (IsGuidKey)
        {
            primaryKey = ToGuidKey(Id_0, Id_1).ToString();
            keyExtension = StringKey;
        }
        else
        {
            primaryKey = StringKey;
        }

        const string GrainIdAndExtensionSeparator = "#";
        return string.Format(
            $"{primaryKey}{(keyExtension != null ? GrainIdAndExtensionSeparator + keyExtension : string.Empty)}"
        );
    }

    private static Guid ToGuidKey(long n0Key, long n1Key)
    {
        return new Guid((uint)(n0Key & 0xffffffff), (ushort)(n0Key >> 32), (ushort)(n0Key >> 48), (byte)n1Key,
            (byte)(n1Key >> 8), (byte)(n1Key >> 16), (byte)(n1Key >> 24), (byte)(n1Key >> 32), (byte)(n1Key >> 40),
            (byte)(n1Key >> 48), (byte)(n1Key >> 56)
        );
    }
}

public static class GrainKeyExtensions
{
    public static GrainKey ToKey(this GrainId id)
    {
        if (id.TryGetGuidKey(out var guid, out var keyExt))
            return new GrainKey(guid, keyExt!);

        if (id.TryGetIntegerKey(out var integer, out keyExt))
            return new GrainKey(integer, keyExt!);

        return new GrainKey(id.Key.ToString());
    }
}