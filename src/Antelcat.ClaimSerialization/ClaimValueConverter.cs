namespace Antelcat.ClaimSerialization;

public abstract class ClaimValueConverter
{
    internal abstract IEnumerable<string> ReadInternal(object value);
    
    internal abstract object? WriteInternal(IEnumerable<string> values);
}