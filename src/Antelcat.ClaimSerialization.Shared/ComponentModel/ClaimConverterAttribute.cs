namespace Antelcat.ClaimSerialization.ComponentModel;

[AttributeUsage(AttributeTargets.Property)]
public class ClaimConverterAttribute(Type converterType) : Attribute;
