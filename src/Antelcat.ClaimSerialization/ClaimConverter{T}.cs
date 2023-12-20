namespace Antelcat.ClaimSerialization;

public abstract class ClaimValueConverter<T> : ClaimValueConverter
{
    internal override IEnumerable<string> ReadInternal(object value)
    {
        if (value is T t)
        {
            return Read(t);
        }
        return Enumerable.Empty<string>();
    }
    internal override object? WriteInternal(IEnumerable<string> values)
    {
        return Write(values);
    }

    public abstract IEnumerable<string> Read(T value);
    
    public abstract T? Write(IEnumerable<string> values);
}