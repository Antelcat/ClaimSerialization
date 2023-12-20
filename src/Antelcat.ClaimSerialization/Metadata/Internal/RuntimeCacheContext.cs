using System.Reflection;
using Antelcat.IL;
using Antelcat.IL.Extensions;

namespace Antelcat.ClaimSerialization.Metadata.Internal;

internal class RuntimeCacheContext
{
    internal static RuntimeCacheContext Default { get; } = new();

    private static readonly Type CollectionType = typeof(ICollection<>);

    private static MethodInfo ToArray { get; } =
        typeof(Enumerable).GetMethod(nameof(Enumerable.ToArray), BindingFlags.Public | BindingFlags.Static)!;

    private static MethodInfo ToList { get; } =
        typeof(Enumerable).GetMethod(nameof(Enumerable.ToList), BindingFlags.Public | BindingFlags.Static)!;

    internal static IReadOnlyDictionary<Type, Func<string, object>> StringConvertableSystemTypes { get; }
        = GetStringConvertableSystemTypes()
            .ToDictionary(static x => x.Key, static x => x.Value);

    internal static IEnumerable<Type> SystemGenericCollectionTypes { get; } =
        typeof(SortedSet<>).Assembly.ExportedTypes
            .Where(PredicateSystemGenericCollection)
            .Reverse()
            .Concat(typeof(object).Assembly.ExportedTypes
                .Where(PredicateSystemGenericCollection))
            .ToList();

    private static IEnumerable<KeyValuePair<Type, Func<string, object>>> GetStringConvertableSystemTypes()
    {
        foreach (var type in typeof(object).Assembly.GetExportedTypes())
        {
            var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (m.Name != nameof(int.Parse)) return false;
                    var param = m.GetParameters();
                    return param.Length              == 1
                           && param[0].ParameterType == typeof(string)
                           && m.ReturnType           == type;
                });
            if (method == null) continue;
            var handler = method.CreateInvoker<object, string>();
            yield return new(type, s => handler(null, s)!);
        }
    }

    private static bool PredicateSystemGenericCollection(Type type)
    {
        if (type is not { IsClass: true, IsGenericType: true, ContainsGenericParameters: true })
            return false;
        var names = type.Name.Split('`');
        if (names.Length != 2                                         ||
            !int.TryParse(type.Name.Split('`').Last(), out var count) || count != 1) return false;
        return EnumAllInterfaces(type)
            .Any(t => t.ContainsGenericParameters
                      && t.GetGenericTypeDefinition() == typeof(ICollection<>));
    }

    internal static IEnumerable<Type> EnumAllInterfaces(Type type)
    {
        var interfaces = type.GetInterfaces();
        foreach (var @interface in interfaces.Concat(interfaces.SelectMany(EnumAllInterfaces)))
        {
            yield return @interface;
        }
    }

    internal ClaimTypeInfo CreateRuntimeTypeInfo(Type type) =>
        (ClaimTypeInfo)RuntimeClaimTypeInfo.MakeGenericMethod(type).Invoke(null, []);

    private static RuntimeClaimTypeInfo<T> CreateRuntimeTypeInfo<T>() => new();

    private MethodInfo RuntimeClaimTypeInfo { get; } =
        typeof(RuntimeCacheContext).GetMethod(nameof(CreateRuntimeTypeInfo),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    internal Dictionary<Type, ClaimTypeInfo>                 CachedClaimTypeInfos   { get; } = new();
    private  Dictionary<Type, InvokeHandler<object, object>> CollectionAdders       { get; } = new();
    private  Dictionary<Type, InvokeHandler<object, object>> ToArrayHandlers        { get; } = new();
    private  Dictionary<Type, InvokeHandler<object, object>> ToListHandlers         { get; } = new();
    private  Dictionary<Type, CtorHandler<object>>           CollectionConstructors { get; } = new();
    private  Dictionary<Type, ClaimValueConverter>           ClaimValueConverters   { get; } = new();

    public InvokeHandler<object, object> GetToArrayHandler(Type elementType) =>
        TryGetOrAdd(ToArrayHandlers, elementType, () => ToArray.MakeGenericMethod(elementType).CreateInvoker());

    public InvokeHandler<object, object> GetToListHandler(Type elementType) =>
        TryGetOrAdd(ToListHandlers, elementType, () => ToList.MakeGenericMethod(elementType).CreateInvoker());

    public InvokeHandler<object, object> GetCollectionAdder(Type elementType) =>
        TryGetOrAdd(CollectionAdders, elementType, () =>
            CollectionType.MakeGenericType(elementType).GetMethod(nameof(ICollection<object>.Add))!.CreateInvoker());

    public CtorHandler<object> GetCollectionConstructor(Type collectionType) =>
        TryGetOrAdd(CollectionConstructors, collectionType, () => collectionType.CreateCtor());

    public ClaimValueConverter GetClaimValueConverter(Type type) =>
        TryGetOrAdd(ClaimValueConverters, type, () => (ClaimValueConverter)Activator.CreateInstance(type));

    private static T TryGetOrAdd<T>(Dictionary<Type, T> source, Type type, Func<T> getter) where T : class
    {
        if (source.TryGetValue(type, out var value)) return value;
        lock (source)
        {
            if (source.TryGetValue(type, out value)) return value;
            var ret = getter();
            source.Add(type, ret);
            return ret;
        }
    }
}