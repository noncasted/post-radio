using System.Text;
using Newtonsoft.Json;

namespace Common.Extensions;

public class PostgresJsonbConverter<T> : JsonConverter<T> where T : class
{
    private static readonly byte[] JsonbMagic = { 0x01 }; // PostgreSQL jsonb format magic byte


    public override T ReadJson(
        JsonReader reader,
        Type objectType,
        T? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null!;

        // If it's already a string or object, deserialize normally
        var json = reader.Value switch
        {
            string str => str,
            byte[] bytes => DecodeBinary(bytes),
            _ => JsonConvert.SerializeObject(reader.Value)
        };

        return JsonConvert.DeserializeObject<T>(json, JsonUtils.Settings) ??
               throw new InvalidOperationException("Failed to deserialize jsonb data");
    }


    public override void WriteJson(JsonWriter writer, T? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        var json = JsonConvert.SerializeObject(value, JsonUtils.Settings);
        writer.WriteValue(EncodeBinary(json));
    }


    private static string DecodeBinary(byte[] data)
    {
        if (data == null || data.Length == 0)
            return "{}";

        // Skip PostgreSQL jsonb magic byte (0x01)
        var startIndex = data[0] == 0x01 ? 1 : 0;
        return Encoding.UTF8.GetString(data, startIndex, data.Length - startIndex);
    }


    private static byte[] EncodeBinary(string json)
    {
        var utf8Bytes = Encoding.UTF8.GetBytes(json);
        var result = new byte[utf8Bytes.Length + 1];
        result[0] = 0x01; // PostgreSQL jsonb magic byte
        Buffer.BlockCopy(utf8Bytes, 0, result, 1, utf8Bytes.Length);
        return result;
    }
}