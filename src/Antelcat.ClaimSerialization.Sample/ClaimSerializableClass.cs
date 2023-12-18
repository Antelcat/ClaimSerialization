using System;
using System.Collections.Generic;
using System.Security.Claims;
using Antelcat.ClaimSerialization.ComponentModel;

namespace Antelcat.ClaimSerialization.Sample;

[ClaimSerializable]
public unsafe partial class ClaimSerializableClass
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

