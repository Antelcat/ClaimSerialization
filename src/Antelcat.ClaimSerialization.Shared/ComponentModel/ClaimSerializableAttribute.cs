namespace Antelcat.ClaimSerialization.ComponentModel;

/// <summary>
/// Types marked this attribute will auto generate implement methods of <see cref="Antelcat.ClaimSerialization.IClaimSerializable"/>
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ClaimSerializableAttribute : Attribute;