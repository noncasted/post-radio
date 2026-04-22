using Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orleans.GrainReferences;

namespace Infrastructure.State;

public interface IStateSerializer
{
    string Serialize(object value);
    string Serialize<T>(T value);
    T Deserialize<T>(string value);
    T? TryDeserialize<T>(string value);
}

public class StateSerializer : IStateSerializer
{
    public StateSerializer(IServiceProvider serviceProvider)
    {
        _settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            MetadataPropertyHandling = MetadataPropertyHandling.ReadAhead,
            PreserveReferencesHandling = PreserveReferencesHandling.None,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            Formatting = Formatting.None,
            SerializationBinder = null,
        };

        _settings.Converters.Add(new GrainIdConverter());

        _settings.Converters.Add(
            new GrainReferenceJsonConverter(serviceProvider.GetRequiredService<GrainReferenceActivator>()));
    }

    private readonly JsonSerializerSettings _settings;

    public string Serialize(object value)
    {
        return JsonConvert.SerializeObject(value, _settings);
    }

    public string Serialize<T>(T value)
    {
        return JsonConvert.SerializeObject(value, typeof(T), _settings);
    }

    public T Deserialize<T>(string value)
    {
        return JsonConvert.DeserializeObject<T>(value, _settings).ThrowIfNull();
    }

    public T? TryDeserialize<T>(string value)
    {
        return JsonConvert.DeserializeObject<T>(value, _settings);
    }
}

public class GrainIdConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) => objectType == typeof(GrainId);

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var id = (GrainId)value!;
        writer.WriteStartObject();
        writer.WritePropertyName("key");
        var key = $"{id.Type}:{id.Key}";
        writer.WriteValue(key);
        writer.WriteEndObject();
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var json = JObject.Load(reader);
        var raw = json["key"]!.ToObject<string>()!;
        var split = raw.Split(':', count: 2);

        if (split.Length != 2)
            throw new JsonSerializationException($"[GrainIdConverter] Invalid GrainId format: '{raw}'");

        var grainId = GrainId.Create(split[0], split[1]);
        return grainId;
    }
}

public class GrainReferenceJsonConverter : JsonConverter
{
    public GrainReferenceJsonConverter(GrainReferenceActivator activator)
    {
        _activator = activator;
    }

    private static readonly Type AddressableType = typeof(IAddressable);
    private readonly GrainReferenceActivator _activator;

    public override bool CanConvert(Type objectType)
    {
        return AddressableType.IsAssignableFrom(objectType);
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {

        GrainReference context = value switch
        {
            GrainReference reference => reference,
            Grain grain => grain.GrainContext.GrainReference,
            IGrainBase grainBase => grainBase.GrainContext.GrainReference,
            _ => throw new InvalidOperationException($"Unsupported type {value?.GetType()} for grain reference")
        };

        var id = context.GrainId;
        var raw = $"{context.InterfaceType.ToString()}:{id.Type.ToString()}:{id.Key.ToString()}";

        writer.WriteValue(raw);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var json = JToken.Load(reader);
        var raw = json.Value<string>()!;
        var split = raw.Split(':', count: 3);

        if (split.Length != 3)
            throw new JsonSerializationException(
                $"[GrainReferenceJsonConverter] Invalid GrainReference format: '{raw}'");

        var encodedInterface = split[0];

        var interfaceType = string.IsNullOrWhiteSpace(encodedInterface)
            ? default
            : GrainInterfaceType.Create(encodedInterface);

        var grainId = GrainId.Create(split[1], split[2]);
        return _activator.CreateReference(grainId, interfaceType);
    }
}