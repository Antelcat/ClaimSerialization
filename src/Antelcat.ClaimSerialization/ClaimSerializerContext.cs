#if !NET46
using System.Text.Json;
using System.Text.Json.Serialization;

using Antelcat.ClaimSerialization.Metadata;

namespace Antelcat.ClaimSerialization;

public abstract class ClaimSerializerContext(JsonSerializerContext? jsonSerializerContext = null)
{
    private Func<object?, Type, string> SerializeObject { get; } =
        jsonSerializerContext == null
            ? (o, type) => JsonSerializer.Serialize(o, type)
            : (o, type) => JsonSerializer.Serialize(o, type, jsonSerializerContext);

    private Func<string, Type, object?> DeserializeObject { get; } =
        jsonSerializerContext == null
            ? (o, type) => JsonSerializer.Deserialize(o, type)
            : (o, type) => JsonSerializer.Deserialize(o, type, jsonSerializerContext);

    public abstract ClaimTypeInfo? GetTypeInfo(Type type);
}
#endif

