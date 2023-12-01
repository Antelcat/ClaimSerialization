using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Antelcat.Attributes;
using Antelcat.ClaimSerialization.SourceGenerators.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Antelcat.ClaimSerialization.Tests;

public class ClaimSerializationGeneratorTests
{
    private const string VectorClassText = @"
using Antelcat.Attributes;

namespace Antelcat.ClaimSerialization.SourceGenerators.Sample;

[ClaimSerializable]
public partial class ClaimSerializableClass
{
    public int Num { get; init; }
    
    public string Str { get; set; } = string.Empty;
}

";

    [Fact]
    public void Generate()
    {
        var s    = JsonSerializer.Serialize('a');
        var path = Path.GetFullPath(@"..\..\..\..\");
        // Create an instance of the source generator.
        var generator = new ClaimSerializeGenerator();
        
        // Source generators should be tested using 'GeneratorDriver'.
        var driver = CSharpGeneratorDriver.Create(generator);

        // We need to create a compilation with the required source code.
        var compilation = CSharpCompilation.Create(nameof(ClaimSerializationGeneratorTests),
            new[]
            {
                CSharpSyntaxTree.ParseText(File.ReadAllText(
                    Path.Combine(path, @"Antelcat.ClaimSerialization.Sample\ClaimSerializableClass.cs")))
            },
            new[]
            {
                // To support 'System.Attribute' inheritance, add reference to 'System.Private.CoreLib'.
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ClaimSerializableAttribute).Assembly.Location)
            });
        // Run generators and retrieve all results.
        var runResult = driver.RunGenerators(compilation).GetRunResult();
        // All generated files can be found in 'RunResults.GeneratedTrees'.
        var generatedFileSyntax = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("Vector3.g.cs"));
    }

}