namespace Antelcat.ClaimSerialization.Metadata;

public class SetProperty(string type, Action<object, object> setter)
{
    public string Type { get; } = type;

    public SetProperty(string type, 
        Action<object, object> setter,
        Func<IEnumerable<string>, object?> dynamicTransform) :
        this(type, setter) => Transform = dynamicTransform;

    private Action<object, object>             Handler   { get; } = setter;
    private Func<IEnumerable<string>, object?> Transform { get; } = s => s.First();

    public void Set(object target, IEnumerable<string> value)
    {
        var val = Transform(value);
        if (val != null) Handler(target, val);
    }
}