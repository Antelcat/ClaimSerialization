using System;
using System.Security.Claims;

namespace Antelcat.ClaimSerialization.ComponentModel;

/// <summary>
/// Replace the <see cref="Claim.Type"/> of the  <see cref="Claim"/>
/// </summary>
/// <param name="type"> Special Types from <see cref="System.Security.Claims.ClaimTypes"/> </param>
[AttributeUsage(AttributeTargets.Property)]
public class ClaimTypeAttribute(string? type = null) : Attribute
{
    internal string? Type { get; } = type;
}
