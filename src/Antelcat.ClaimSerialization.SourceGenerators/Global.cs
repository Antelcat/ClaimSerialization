global using System;
global using static Antelcat.ClaimSerialization.SourceGenerators.Diagnostics;

namespace Antelcat.ClaimSerialization.SourceGenerators;

internal static class Diagnostics
{
    public const string CS0001 = "[{0}] should not be marked with [{1}]";
    public const string CS0002 = "Model marked with [{0}] cannot have { init } properties";
    public const string CS0003 = "Converter [{0}] should have no argument constructor";
}
