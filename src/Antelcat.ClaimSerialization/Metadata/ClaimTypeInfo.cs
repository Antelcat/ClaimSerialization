using System.Collections;
using System.Reflection;
using System.Security.Claims;
using Antelcat.ClaimSerialization.ComponentModel;
using Antelcat.ClaimSerialization.Metadata.Internal;
using Antelcat.IL;
using Antelcat.IL.Extensions;

namespace Antelcat.ClaimSerialization.Metadata;

public class ClaimTypeInfo
{
    protected class SetHandlers
    {
        public SetHandlers(SetHandler<object, object> handler) => Handler = handler;

        public SetHandlers(SetHandler<object, object> handler,
            InvokeHandler<object, IEnumerable<string>> staticTransform)
        {
            Handler   = handler;
            Transform = s => staticTransform(null, s)!;
        }

        public SetHandlers(SetHandler<object, object> handler, Func<IEnumerable<string>, object?> dynamicTransform)
        {
            Handler   = handler;
            Transform = dynamicTransform;
        }

        private SetHandler<object, object>         Handler   { get; }
        private Func<IEnumerable<string>, object?> Transform { get; } = s => s.First();

        public void Set(object target, IEnumerable<string> value)
        {
            var val = Transform(value);
            if (val != null) Handler(ref target!, val);
        }
    }

    protected class GetHandlers
    {
        public GetHandlers(GetHandler<object, object> handler)
        {
            Handler = handler;
        }

        public GetHandlers(GetHandler<object, object> handler, Func<object, IEnumerable<string>> dynamicTransform)
        {
            Handler   = handler;
            Transform = dynamicTransform;
        }

        private GetHandler<object, object>        Handler   { get; }
        private Func<object, IEnumerable<string>> Transform { get; } = Default;

        public IEnumerable<string> Get(object target)
        {
            var val = Handler(target);
            return val == null ? Array.Empty<string>() : Transform(val);
        }

        private static IEnumerable<string> Default(object o)
        {
            yield return o.ToString();
        }
    }

    private readonly Dictionary<string, SetHandlers> setHandlers;
    private readonly Dictionary<string, GetHandlers> getHandlers;
    private readonly CtorHandler<object>             ctorHandler;

    protected ClaimTypeInfo(Type type)
    {
        var ctor = type.GetConstructors().FirstOrDefault(x => x.GetParameters().Length == 0);
        if (ctor == null)
        {
            throw new MissingMethodException("Can not find default constructor.");
        }

        ctorHandler = ctor.CreateCtor();
        var claimProps = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.GetCustomAttribute(typeof(ClaimIgnoreAttribute)) == null)
            .ToList();
        try
        {
            setHandlers = ResolveSetHandlers(claimProps).ToDictionary(x => x.Key, x => x.Value);
            getHandlers = ResolveGetHandlers(claimProps).ToDictionary(x => x.Key, x => x.Value);
        }
        catch (ArgumentException e)
        {
            throw new ArgumentException($"Duplicate claim type: {e.Message}", e);
        }
    }

    public IEnumerable<Claim> Serialize(object target)
    {
        foreach (var values in getHandlers)
        {
            foreach (var value in values.Value.Get(target))
            {
                if (value != null) yield return new Claim(values.Key, value);
            }
        }
    }
    public object? Deserialize(IEnumerable<Claim> claims)
    {
        var target = ctorHandler();
        foreach (var claim in claims.GroupBy(x => x.Type))
        {
            if (setHandlers.TryGetValue(claim.Key, out var handler))
            {
                handler.Set(target!, claim.Select(x => x.Value));
            }
        }

        return target;
    }

    internal static ClaimTypeInfo GetTypeInfo<T>() => GetTypeInfo(typeof(T));
    internal static ClaimTypeInfo GetTypeInfo(Type type)
    {
        if (CacheContext.Default.CachedClaimTypeInfos.TryGetValue(type, out var info)) return info;
        info = new ClaimTypeInfo(type);
        CacheContext.Default.CachedClaimTypeInfos.Add(type, info);
        return info;
    }

    private static string GetClaimType(MemberInfo property)
    {
        var claimType = property.Name;
        var attr = property.CustomAttributes
            .FirstOrDefault(x => x.AttributeType == typeof(ClaimTypeAttribute));
        if (attr != null)
        {
            claimType = attr.ConstructorArguments[0].Value as string ?? property.Name;
        }

        return claimType;
    }
    
    protected static IEnumerable<KeyValuePair<string, SetHandlers>> ResolveSetHandlers(
        IEnumerable<PropertyInfo> properties) =>
        from property
            in properties.Where(x => x.CanWrite)
        let type = GetClaimType(property)
        select new KeyValuePair<string, SetHandlers>(type,
            ResolveSetHandler(property.PropertyType, property.CreateSetter()));
    protected static IEnumerable<KeyValuePair<string, GetHandlers>> ResolveGetHandlers(
        IEnumerable<PropertyInfo> properties) =>
        from property
            in properties.Where(x => x.CanRead)
        let type = GetClaimType(property)
        select new KeyValuePair<string, GetHandlers>(type,
            ResolveGetHandler(property.PropertyType, property.CreateGetter()));

    private static GetHandlers ResolveGetHandler(Type propertyType, GetHandler<object, object> getter)
    {
        if (propertyType    == typeof(string)
            || propertyType == typeof(object)
            || propertyType.IsEnum
            || CacheContext.StringConvertableSystemTypes.ContainsKey(propertyType))
            return new GetHandlers(getter);

        if (CanBeIEnumerable(propertyType, out var elementType))
        {
            var select = GetSelect(elementType!);
            return new GetHandlers(getter, x => select((IEnumerable)x)!);
        }

        var serialize = Serialize(propertyType);
        return new GetHandlers(getter, x => Yield(serialize(x)));
    }
    private static SetHandlers ResolveSetHandler(Type propertyType, SetHandler<object, object> setter)
    {
        if (propertyType == typeof(string) || propertyType == typeof(object))
            return new SetHandlers(setter);
        if (propertyType.IsEnum)
            return new SetHandlers(setter, s => Enum.Parse(propertyType, s.First()));

        if (CacheContext.StringConvertableSystemTypes.TryGetValue(propertyType, out var handler))
            return new SetHandlers(setter, s => handler(s.First()));
        if (CanBeIEnumerable(propertyType, out var elementType))
        {
            var select = SetSelect(elementType!);
            var to     = MapTo(propertyType, elementType!);
            return new SetHandlers(setter, x => to(select(x)));
        }

        var serialize = Deserialize(propertyType);
        return new SetHandlers(setter, x => serialize(x.First()));
    }

    private static Func<IEnumerable, IEnumerable<string?>> GetSelect(Type elementType)
    {
        if (elementType    == typeof(string)
            || elementType == typeof(object)
            || elementType.IsEnum
            || CacheContext.StringConvertableSystemTypes.ContainsKey(elementType))
            return enumerable =>
                enumerable.Cast<object>().Select(x => x?.ToString());

        return enumerable => ((IEnumerable<object>)enumerable).Select(Serialize(elementType));
    }
    private static Func<IEnumerable<string>, IEnumerable<object?>> SetSelect(Type elementType)
    {
        if (elementType == typeof(string) || elementType == typeof(object))
            return enumerable => enumerable;

        if (elementType.IsEnum)
            return enumerable => enumerable.Select(x => Enum.Parse(elementType, x));

        if (CacheContext.StringConvertableSystemTypes.TryGetValue(elementType, out var handler))
            return enumerable => enumerable.Select(handler);

        return enumerable => enumerable.Select(Deserialize(elementType));
    }

    private static IEnumerable<string> Yield(string o)
    {
        yield return o;
    }

    private static Func<string, object?> Deserialize(Type valueType) => x => x.JsonDeserialize(valueType);
    private static Func<object?, string> Serialize(Type valueType) => x => x.JsonSerialize(valueType);

    public static bool CanBeIEnumerable(Type type, out Type? elementType)
    {
        elementType = null;
        if (type.IsArray) return true;
        if (type.IsInterface)
        {
            if (Selector(type, out elementType))
            {
                return true;
            }
        }

        foreach (var @interface in CacheContext.EnumAllInterfaces(type))
        {
            if (Selector(@interface, out elementType))
            {
                return true;
            }
        }

        return false;

        static bool Selector(Type t, out Type? elementType)
        {
            elementType = null;
            if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(IEnumerable<>)) return false;
            elementType = t.GetGenericArguments()[0];
            return true;
        }
    }

    private static Func<IEnumerable<object?>, object> MapToCollection(Type collectionType, Type elementType)
    {
        var add  = CacheContext.Default.GetCollectionAdder(elementType);
        var ctor = CacheContext.Default.GetCollectionConstructor(collectionType);
        return x =>
        {
            var set = ctor()!;
            foreach (var o in x)
            {
                add(set, o);
            }

            return set;
        };
    }
    private static Func<IEnumerable<object?>, object> MapTo(Type containerType, Type elementType)
    {
        if (containerType.IsArray) // is T[]
        {
            var toArray = CacheContext.Default.GetToArrayHandler(elementType);
            return x => toArray(null, x)!;
        }

        if (containerType is { IsClass: true, IsGenericType: true })
        {
            var genericTypeDefinition = containerType.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(List<>)) // is List<T>
            {
                var toList = CacheContext.Default.GetToListHandler(elementType);
                return x => toList(null, x)!;
            }

            if (CacheContext.SystemGenericCollectionTypes.Any(x =>
                    x.MakeGenericType(elementType) == containerType)) // is System Generic Collection
            {
                return MapToCollection(containerType, elementType);
            }

            // is custom collection
            if (typeof(ICollection<>).MakeGenericType(elementType).IsAssignableFrom(containerType))
            {
                return MapToCollection(containerType, elementType);
            }
        }

        if (containerType.IsInterface)
        {
            if (containerType.IsAssignableFrom(typeof(List<>).MakeGenericType(elementType))) // can be List<T>
            {
                var toList = CacheContext.Default.GetToListHandler(elementType);
                return x => toList(null, x)!;
            }

            var type = CacheContext.SystemGenericCollectionTypes
                .Select(x => x.MakeGenericType(elementType))
                .FirstOrDefault(containerType.IsAssignableFrom);

            if (type != null)
            {
                return MapToCollection(type, elementType);
            }
        }

        throw new ArgumentException("Not supported type.");
    }
}