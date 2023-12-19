using System.Security.Claims;
using Antelcat.ClaimSerialization.Metadata.Internal;

namespace Antelcat.ClaimSerialization.Metadata;

public abstract class ClaimTypeInfo
{
    protected abstract Constructor   Constructor { get; }
    protected abstract SetProperty[] Setters     { get; }
    protected abstract GetProperty[] Getters     { get; }


    public IEnumerable<Claim> Serialize(object target)
    {
        foreach (var values in Getters)
        {
            foreach (var value in values.Get(target))
            {
                if (value != null) yield return new Claim(values.Type, value);
            }
        }
    }

    public object? Deserialize(IEnumerable<Claim> claims)
    {
        var target = Constructor.Create();
        foreach (var claim in claims.GroupBy(x => x.Type))
        {
            var handler = Setters.FirstOrDefault(x => x.Type == claim.Key);
            handler?.Set(target!, claim.Select(x => x.Value));
        }

        return target;
    }

    internal static ClaimTypeInfo GetTypeInfo<T>()
    {
        var type = typeof(T);
        if (CacheContext.Default.CachedClaimTypeInfos.TryGetValue(type, out var info)) return info;
        info = new RuntimeClaimTypeInfo<T>();
        CacheContext.Default.CachedClaimTypeInfos.Add(type, info);
        return info;
    }

    internal static ClaimTypeInfo GetTypeInfo(Type type)
    {
        if (CacheContext.Default.CachedClaimTypeInfos.TryGetValue(type, out var info)) return info;
        info = (ClaimTypeInfo)Activator.CreateInstance(typeof(RuntimeClaimTypeInfo<>).MakeGenericType(type));
        CacheContext.Default.CachedClaimTypeInfos.Add(type, info);
        return info;
    }
}