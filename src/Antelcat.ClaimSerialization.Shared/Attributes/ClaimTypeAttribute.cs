using System;
using System.Security.Claims;

namespace Antelcat.ClaimSerialization.Attributes;

/// <summary>
/// Replace the <see cref="Claim.Type"/> of the  <see cref="Claim"/>
/// </summary>
/// <param name="Type"> Special Types from <see cref="System.Security.Claims.ClaimTypes"/> </param>
[AttributeUsage(AttributeTargets.Property)]
public class ClaimTypeAttribute(string? Type = null) : Attribute;
