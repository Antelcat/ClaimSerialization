﻿namespace Antelcat.ClaimSerialization.ComponentModel;

/// <summary>
/// Properties marked this attribute will not be mapped into claims
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ClaimIgnoreAttribute : Attribute;