using System.Security.Claims;
using Antelcat.ClaimSerialization.Metadata;

#if NET46
using Newtonsoft.Json;
#else
using System.Text.Json;
#endif

namespace Antelcat.ClaimSerialization;

public static class ClaimSerializer
{
    internal static string JsonSerialize(this object? o, Type type) =>
#if NET46
        JsonConvert.SerializeObject(o, type, null);
#else
        JsonSerializer.Serialize(o, type);
#endif

    internal static object? JsonDeserialize<T>(this string json) =>
#if NET46
        JsonConvert.DeserializeObject(json);
#else
        JsonSerializer.Deserialize<T>(json);
#endif

    internal static object? JsonDeserialize(this string json, Type type) =>
#if NET46
        JsonConvert.DeserializeObject(json,type);
#else
        JsonSerializer.Deserialize(json, type);
#endif

    public static IEnumerable<Claim> Serialize(object? value, Type? type)
    {
        if (value == null) yield break;
        foreach (var claim in ClaimTypeInfo.GetTypeInfo(type!).Serialize(value))
        {
            yield return claim;
        }
    }

    public static IEnumerable<Claim> Serialize(object? value) => Serialize(value, value?.GetType());
    public static IEnumerable<Claim> Serialize<T>(T value) => Serialize(value, typeof(T));

    public static T? Deserialize<T>(IEnumerable<Claim> claims) where T : notnull =>
        (T?)ClaimTypeInfo.GetTypeInfo<T>().Deserialize(claims);

    public static object? Deserialize(IEnumerable<Claim> claims, Type type) =>
        ClaimTypeInfo.GetTypeInfo(type).Deserialize(claims);
}