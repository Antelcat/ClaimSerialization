namespace Antelcat.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class ClaimConverterAttribute(Type converterType) : Attribute;
