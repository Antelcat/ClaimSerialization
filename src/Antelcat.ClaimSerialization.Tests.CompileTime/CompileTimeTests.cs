using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Antelcat.ClaimSerialization.ComponentModel;
using Antelcat.ClaimSerialization.SourceGenerators.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Antelcat.ClaimSerialization.Tests.CompileTime;

public class CompileTimeTests
{
    [Fact]
    public void Generate()
    {
        var path = Path.GetFullPath(@"..\..\..\..\");
        // Create an instance of the source generator.
        var generator = new ClaimSerializeGenerator();
        
        // Source generators should be tested using 'GeneratorDriver'.
        var driver = CSharpGeneratorDriver.Create(generator);

        // We need to create a compilation with the required source code.
        var compilation = CSharpCompilation.Create(nameof(CompileTimeTests),
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

    public class Foo : JsonConverter<string>
    {
        public Foo(string str)
        {
            
        }

        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
   
}