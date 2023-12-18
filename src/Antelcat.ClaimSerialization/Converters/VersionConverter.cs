using System.Security.Claims;

namespace Antelcat.ClaimSerialization.Converters;

public class VersionConverter : ClaimConverter<Version>
{
    public override IEnumerable<string> Read(Version value)
    {
        yield return value.ToString();
    }

    public override Version? Write(IEnumerable<string> claims) =>
        Version.TryParse(claims.FirstOrDefault() ?? string.Empty, out var result) 
            ? result 
            : default;
}