namespace Antelcat.ClaimSerialization.Converters;

public class GuidConverter : ClaimConverter<Guid>
{
    public override IEnumerable<string> Read(Guid value)
    {
        yield return value.ToString();
    }

    public override Guid Write(IEnumerable<string> claims) =>
        Guid.TryParse(claims.FirstOrDefault() ?? string.Empty, out var result)
            ? result
            : default;
}