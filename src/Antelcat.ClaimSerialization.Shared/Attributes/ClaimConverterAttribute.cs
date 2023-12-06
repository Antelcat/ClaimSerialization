namespace Antelcat.ClaimSerialization.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class ClaimConverterAttribute(Type converterType) : Attribute;
