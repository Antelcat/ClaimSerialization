namespace Antelcat.ClaimSerialization.Metadata;

public class Constructor(Func<object[],object?> constructor)
{
    public object? Create(params object[] args) => constructor(args);
}
