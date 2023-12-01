using System.Collections.Generic;
using System.Security.Claims;
using Antelcat.Attributes;

namespace Antelcat.ClaimSerialization.Sample;

[ClaimSerializable]
public partial class ClaimSerializableClass
{
    [ClaimIgnore]
    public ICollection<string>? Static { get; init; }

    public int Num { get; set; }

    public string Str { get; set; } = string.Empty;

}