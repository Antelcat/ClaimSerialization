using System;
using System.Security.Claims;
using Antelcat.ClaimSerialization.ComponentModel;
using Antelcat.ClaimSerialization.Metadata;

namespace Antelcat.ClaimSerialization.Sample;

public partial class ClaimSerializableClass
{
    
    [ClaimType(ClaimTypes.Role)]
    public string Static { get; set; }

    public int Num { get; set; }

    public string Str { get; set; } = string.Empty;
    
    public Version? Version { get; set; }
    
    public E Enum { get; set; }
    
    public enum E
    {
        A,
        B,
        C
    }

}
[ClaimSerializable(typeof(ClaimSerializableClass))]
public partial class ClaimSerializationClass : ClaimSerializerContext
{
    public override ClaimTypeInfo? GetTypeInfo(Type type)
    {
        throw new NotImplementedException();
    }
}
