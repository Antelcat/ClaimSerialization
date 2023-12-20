using System.Collections;
using System.Reflection;
using Antelcat.ClaimSerialization.ComponentModel;
using Antelcat.IL.Extensions;

namespace Antelcat.ClaimSerialization.Metadata.Internal;

internal sealed class RuntimeClaimTypeInfo<T> : ClaimTypeInfo
{
    protected override Constructor   Constructor { get; }
    protected override SetProperty[] Setters     { get; }
    protected override GetProperty[] Getters     { get; }

    public RuntimeClaimTypeInfo()
    {
        var type = typeof(T);
        var ctor = type
            .GetConstructors()
            .FirstOrDefault(x => x.GetParameters().Length == 0);
        if (ctor == null) throw new MissingMethodException("Can not find default constructor.");

        var claimProps = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(x => x.GetCustomAttribute(typeof(ClaimIgnoreAttribute)) == null)
            .ToList();
        var ctorH = ctor.CreateCtor();
        Constructor = new Constructor(x => ctorH(x));
        Setters     = ResolveSetHandlers(claimProps).ToArray();
        Getters     = ResolveGetHandlers(claimProps).ToArray();
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

    private static IEnumerable<SetProperty> ResolveSetHandlers(
        IEnumerable<PropertyInfo> properties) =>
        from property in properties.Where(x => x.CanWrite)
        select ResolveSetHandler(property);

    private static IEnumerable<GetProperty> ResolveGetHandlers(
        IEnumerable<PropertyInfo> properties) =>
        from property in properties.Where(x => x.CanRead)
        select ResolveGetHandler(property);

    private static GetProperty ResolveGetHandler(PropertyInfo propertyInfo)
    {
        var claimType    = GetClaimType(propertyInfo);
        var getter       = propertyInfo.CreateGetter();
        var propertyType = propertyInfo.PropertyType;
        if (propertyType    == typeof(string)
            || propertyType == typeof(object)
            || propertyType.IsEnum
            || RuntimeCacheContext.StringConvertableSystemTypes.ContainsKey(propertyType))
            return new GetProperty(claimType, x => getter(x)!);

        if (CanBeIEnumerable(propertyType, out var elementType))
        {
            var select = GetSelect(elementType!);
            return new GetProperty(claimType, x => getter(x)!, x => select((IEnumerable)x)!);
        }

        var serialize = Serialize(propertyType);
        return new GetProperty(claimType, x => getter(x)!, x => Yield(serialize(x)));
    }

    private static SetProperty ResolveSetHandler(PropertyInfo propertyInfo)
    {
        var claimType    = GetClaimType(propertyInfo);
        var setter       = propertyInfo.CreateSetter();
        var setterFun    = (Action<object, object>)((o, v) => setter(ref o!, v));
        var propertyType = propertyInfo.PropertyType;
        if (propertyType == typeof(string) || propertyType == typeof(object))
            return new SetProperty(claimType, setterFun);
        if (propertyType.IsEnum)
            return new SetProperty(claimType, setterFun, s => Enum.Parse(propertyType, s.First()));

        if (RuntimeCacheContext.StringConvertableSystemTypes.TryGetValue(propertyType, out var handler))
            return new SetProperty(claimType, setterFun, s => handler(s.First()));
        if (CanBeIEnumerable(propertyType, out var elementType))
        {
            var select = SetSelect(elementType!);
            var to     = MapTo(propertyType, elementType!);
            return new SetProperty(claimType, setterFun, x => to(select(x)));
        }

        var serialize = Deserialize(propertyType);
        return new SetProperty(claimType, setterFun, x => serialize(x.First()));
    }

    private static Func<IEnumerable, IEnumerable<string?>> GetSelect(Type elementType)
    {
        if (elementType    == typeof(string)
            || elementType == typeof(object)
            || elementType.IsEnum
            || RuntimeCacheContext.StringConvertableSystemTypes.ContainsKey(elementType))
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

        if (RuntimeCacheContext.StringConvertableSystemTypes.TryGetValue(elementType, out var handler))
            return enumerable => enumerable.Select(handler);

        return enumerable => enumerable.Select(Deserialize(elementType));
    }

    private static IEnumerable<string> Yield(string o)
    {
        yield return o;
    }

    private static Func<string, object?> Deserialize(Type valueType) => x => x.JsonDeserialize(valueType);
    private static Func<object?, string> Serialize(Type valueType) => x => x.JsonSerialize(valueType);

    private static bool CanBeIEnumerable(Type type, out Type? elementType)
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

        foreach (var @interface in RuntimeCacheContext.EnumAllInterfaces(type))
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
        var add  = RuntimeCacheContext.Default.GetCollectionAdder(elementType);
        var ctor = RuntimeCacheContext.Default.GetCollectionConstructor(collectionType);
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
            var toArray = RuntimeCacheContext.Default.GetToArrayHandler(elementType);
            return x => toArray(null, x)!;
        }

        if (containerType is { IsClass: true, IsGenericType: true })
        {
            var genericTypeDefinition = containerType.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(List<>)) // is List<T>
            {
                var toList = RuntimeCacheContext.Default.GetToListHandler(elementType);
                return x => toList(null, x)!;
            }

            if (RuntimeCacheContext.SystemGenericCollectionTypes.Any(x =>
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
                var toList = RuntimeCacheContext.Default.GetToListHandler(elementType);
                return x => toList(null, x)!;
            }

            var type = RuntimeCacheContext.SystemGenericCollectionTypes
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