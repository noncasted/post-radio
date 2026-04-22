using Newtonsoft.Json;

namespace Common.Extensions;

public static class JsonUtils
{
    public static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        TypeNameHandling = TypeNameHandling.All,
    };

    private static readonly JsonSerializerSettings _options = Settings;


    public static string Serialize(object value)
    {
        return JsonConvert.SerializeObject(value, _options);
    }

    public static T Deserialize<T>(string raw)
    {
        return JsonConvert.DeserializeObject<T>(raw, _options) ??
               throw new InvalidOperationException($"Failed to deserialize JSON into {typeof(T)}");
    }
}