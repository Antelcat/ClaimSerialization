namespace Antelcat.ClaimSerialization;

public abstract class ClaimConverter<T>
{
    public abstract IEnumerable<string> Read(T value);
    
    public abstract T? Write(IEnumerable<string> claims);
}