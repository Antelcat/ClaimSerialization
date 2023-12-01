global using System;
global using static Antelcat.ClaimSerialization.SourceGenerators.Diagnostics;

namespace Antelcat.ClaimSerialization.SourceGenerators;

internal static class Diagnostics
{
    public const string CS0001 = "[{0}] should not be marked with [{1}]";
    public const string CS0002 = "Property marked with [{0}] cannot be init or required";
}

internal class Global
{
}