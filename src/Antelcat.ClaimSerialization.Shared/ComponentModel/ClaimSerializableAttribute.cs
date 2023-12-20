#if !NET46
namespace Antelcat.ClaimSerialization.ComponentModel;

/// <summary>
/// Types marked this attribute will auto generate implement methods of <see cref="Antelcat.ClaimSerialization.IClaimSerializable"/>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ClaimSerializableAttribute(Type type) : Attribute
{
    internal Type Type { get; } = type;
}
#endif