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

    public virtual object? Deserialize(IEnumerable<Claim> claims)
    {
        var target = Constructor.Create();
        foreach (var claim in claims.GroupBy(x => x.Type)) 
            Setters
                .FirstOrDefault(x => x.Type == claim.Key)?
                .Set(target!, claim.Select(x => x.Value));

        return target;
    }

    internal static ClaimTypeInfo GetTypeInfo<T>()
    {
        var type = typeof(T);
        if (RuntimeCacheContext.Default.CachedClaimTypeInfos.TryGetValue(type, out var info)) return info;
        lock (RuntimeCacheContext.Default.CachedClaimTypeInfos)
        {
            if (RuntimeCacheContext.Default.CachedClaimTypeInfos.TryGetValue(type, out info)) return info;
            info = new RuntimeClaimTypeInfo<T>();
            RuntimeCacheContext.Default.CachedClaimTypeInfos.Add(type, info);
        }
        return info;
    }

    internal static ClaimTypeInfo GetTypeInfo(Type type)
    {
        if (RuntimeCacheContext.Default.CachedClaimTypeInfos.TryGetValue(type, out var info)) return info;
        lock (RuntimeCacheContext.Default.CachedClaimTypeInfos)
        {
            if (RuntimeCacheContext.Default.CachedClaimTypeInfos.TryGetValue(type, out info)) return info;
            info = RuntimeCacheContext.Default.CreateRuntimeTypeInfo(type);
            RuntimeCacheContext.Default.CachedClaimTypeInfos.Add(type, info);
        }
        return info;
    }

}