namespace Antelcat.ClaimSerialization.Attributes;

/// <summary>
/// Types marked this attribute will auto generate implement methods of <see cref="Antelcat.Interfaces.IClaimSerializable"/>
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ClaimSerializableAttribute : Attribute;