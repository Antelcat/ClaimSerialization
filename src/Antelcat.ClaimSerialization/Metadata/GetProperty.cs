namespace Antelcat.ClaimSerialization.Metadata;

public class GetProperty(string type, Func<object,object> getter)
{
    public string Type { get; } = type;

    public GetProperty(string type, Func<object, object> getter,
        Func<object, IEnumerable<string>> dynamicTransform) : this(type, getter) =>
        Transform = dynamicTransform;

    private Func<object, object>              Handler   { get; } = getter;
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