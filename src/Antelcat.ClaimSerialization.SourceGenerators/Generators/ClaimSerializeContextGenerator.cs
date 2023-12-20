using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Antelcat.ClaimSerialization.ComponentModel;
using Feast.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Assembly = Feast.CodeAnalysis.CompileTime.Assembly;


namespace Antelcat.ClaimSerialization.SourceGenerators.Generators
{
    [Generator]
    public class ClaimSerializeContextGenerator : IIncrementalGenerator
    {
        private static readonly string Attribute          = $"{typeof(ClaimSerializableAttribute).FullName}";
        private static readonly string IClaimSerializable = $"global::{typeof(IClaimSerializable).FullName}";
        private const           string Claim              = "global::System.Security.Claims.Claim";
        private const           string JsonSerializer     = "global::System.Text.Json.JsonSerializer";
        private const           string IEnumerable        = "global::System.Collections.Generic.IEnumerable";

        private const string group = nameof(group);

        private static readonly IEnumerable<Type> ParsableTypes = typeof(object)
            .Assembly
            .GetExportedTypes()
            .Where(x => x.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (m.Name != nameof(int.Parse)) return false;
                    var param = m.GetParameters();
                    return param.Length == 1 && param[0].ParameterType == typeof(string);
                }) != null);


        private static string GetClaimValueTypes(ITypeSymbol symbol)
        {
            return symbol.SpecialType switch
            {
                SpecialType.System_Boolean => ClaimTypes(nameof(ClaimValueTypes.Boolean)),
                SpecialType.System_String or SpecialType.System_Char => ClaimTypes(nameof(ClaimValueTypes.String)),
                SpecialType.System_DateTime => ClaimTypes(nameof(ClaimValueTypes.DateTime)),
                >= SpecialType.System_SByte and <= SpecialType.System_Int16 => ClaimTypes(nameof(ClaimValueTypes.Integer)),
                SpecialType.System_Double => ClaimTypes(nameof(ClaimValueTypes.Double)),
                SpecialType.System_Int32 => ClaimTypes(nameof(ClaimValueTypes.Integer32)),
                SpecialType.System_UInt32 => ClaimTypes(nameof(ClaimValueTypes.UInteger32)),
                SpecialType.System_Int64 => ClaimTypes(nameof(ClaimValueTypes.Integer64)),
                SpecialType.System_UInt64 => ClaimTypes(nameof(ClaimValueTypes.UInteger64)),
                _ => "null"
            };
            string ClaimTypes(string type) => $"global::System.Security.Claims.ClaimValueTypes.{type}";
        }

        private static string GeneratePropToClaim(
            string claimType,
            ITypeSymbol propertyType,
            string propertyName)
        {
            var type = propertyType.ToType();
            if (propertyType.IsJsonBool()
                || propertyType.IsJsonNumber()
                || propertyType.IsJsonString()
                || propertyType.TypeKind == TypeKind.Enum
                || ParsableTypes.Any(x => type.Equals(x)))
            {
                return
                    $"yield return new {Claim}({claimType}, $\"{{{propertyName}}}\", {GetClaimValueTypes(propertyType)});";
            }

            var enumerable = typeof(IEnumerable<>);
            if (!type.IsAssignableTo(enumerable))
                return $"yield return new {Claim}({claimType}, {JsonSerializer}.Serialize({propertyName}));";
            var comp = propertyType
                .Interfaces
                .FirstOrDefault(x =>
                    x.GetFullyQualifiedName() == enumerable.FullName
                    && x.TypeArguments is [INamedTypeSymbol]) ?? (propertyType as INamedTypeSymbol)!;
            var argument = (comp.TypeArguments[0] as INamedTypeSymbol)!;

            return $$"""
                     foreach (var item in {{propertyName}})
                     {
                         {{GeneratePropToClaim(claimType, argument, "item")}}
                     }
                     """;
        }

        private static string GenerateClaimToProp(
            string propName,
            string argumentName,
            ITypeSymbol propertyType, bool inner = false)
        {
            var type = propertyType.ToType();
            // String 直接使用
            if (propertyType.SpecialType == SpecialType.System_String)
            {
                return $"{(!inner ? $"{propName} = " : string.Empty)}{argumentName}";
            }

            // 可转换类型
            if (propertyType.IsJsonBool()
                || propertyType.IsJsonNumber()
                || propertyType.IsJsonString()
                || propertyType.TypeKind == TypeKind.Enum
                || ParsableTypes.Any(x => type.Equals(x)))
            {
                return $"{(!inner ? $"{propName} = " : string.Empty)}{
                    (propertyType.TypeKind != TypeKind.Enum ? propertyType.GetFullyQualifiedName() : $"global::{typeof(Enum).FullName}")
                }.Parse{(
                    propertyType.TypeKind == TypeKind.Enum ? $"<{propertyType.GetFullyQualifiedName()}>" : string.Empty)}({argumentName})";
            }

            var enumerable = typeof(IEnumerable<>);
            if (!type.IsAssignableTo(enumerable))
                return
                    $"{(!inner ? $"{propName} = " : string.Empty)}{JsonSerializer}.Deserialize<{propertyType.GetFullyQualifiedName()}>({argumentName})";
            var comp = propertyType
                .Interfaces
                .FirstOrDefault(x =>
                    x.GetFullyQualifiedName() == enumerable.FullName
                    && x.TypeArguments is [INamedTypeSymbol]) ?? (propertyType as INamedTypeSymbol)!;
            var argument = (comp.TypeArguments[0] as INamedTypeSymbol)!;
            var suffix = propertyType.SpecialType switch
            {
                SpecialType.System_Array => ".ToArray()",
                _                        => ".ToList()"
            };

            return
                $"{(!inner ? $"{propName} = " : string.Empty)}{group}.Select(x=> {GenerateClaimToProp(propName, "x.Value", argument, true)}){suffix}";
        }

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var provider = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    Attribute,
                    (s, _) => s is ClassDeclarationSyntax,
                    (ctx, _) => ctx);

            context.RegisterSourceOutput(
                context.CompilationProvider.Combine(provider.Collect()),
                (ctx, t) =>
                {
                    foreach (var syntax in t.Right)
                    {
                        // We need to get semantic model of the class to retrieve metadata.
                        var semanticModel = t.Left.GetSemanticModel(syntax.TargetNode.SyntaxTree);
                        // Symbols allow us to get the compile-time information.
                        if (semanticModel.GetDeclaredSymbol(syntax.TargetNode) is not INamedTypeSymbol
                            classSymbol)
                            continue;
                        var type          = (syntax.TargetSymbol as INamedTypeSymbol).ToType();
                        var ass           = type.Assembly;
                        var attr          = syntax.Attributes.First().ToAttribute<ClaimSerializableAttribute>();
                        if (type.Assembly.Equals(attr.Type.Assembly))
                        {

                        }

                        var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();

                        // 'Identifier' means the token of the node. Get class name from the syntax node.
                        var className = (syntax.TargetSymbol as INamedTypeSymbol).Name;

                        // Go through all class members with a particular type (property) to generate method lines.
                        var transform = classSymbol.GetMembers()
                            .OfType<IPropertySymbol>()
                            .Where(x => FilterAttribute(x, ctx))
                            .Select(x => Transform(x, ctx))
                            .ToList();
                        var propToClaims = transform
                            .Where(x => !x.typeSymbol.IsReadOnly)
                            .Select(p => p.converter == null
                                ? GeneratePropToClaim(p.claimType, p.typeSymbol.Type, p.propName)
                                : $"yield return new {Claim}({p.claimType}, new {p.converter.GetFullyQualifiedName()}().ConvertToString({p.propName}));"
                            );

                        var claimsToProps = transform
                            .Where(x =>
                            {
                                if (x.typeSymbol.IsInitOnly())
                                {
                                    ctx.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                                        nameof(CS0002),
                                        "Error",
                                        string.Format(CS0002, nameof(ClaimTypeAttribute)),
                                        nameof(ClaimSerializableAttribute),
                                        DiagnosticSeverity.Error,
                                        true
                                    ), x.typeSymbol.Locations.First()));
                                }

                                return !x.typeSymbol.IsWriteOnly;
                            })
                            .Select(p => $"case {p.claimType} :"
                                         + (p.converter == null
                                             ? GenerateClaimToProp(p.propName, $"{group}.First().Value",
                                                 p.typeSymbol.Type)
                                             : $"{p.propName} = ({p.typeSymbol.Type.GetFullyQualifiedName()})new {p.converter.GetFullyQualifiedName()}().ConvertFromString({group}.First().Value)")
                                         + "; break;");

                        var head = TriviaList(
                            Comment("// <auto-generated/> By Antelcat.ClaimSerialization.SourceGenerators"),
                            Trivia(NullableDirectiveTrivia(Token(SyntaxKind.EnableKeyword), true)),
                            Trivia(PragmaWarningDirectiveTrivia(Token(SyntaxKind.DisableKeyword), true)));

                        var codes = CompilationUnit()
                            .AddUsings( //using
                                "System",
                                "System.Collections.Generic",
                                "System.Linq",
                                "System.ComponentModel"
                            ).WithLeadingTrivia(head)
                            .AddMembers( // namespace
                                namespaceName.ToNameSyntax().ToNamespaceDeclaration()
                                    .AddMembers( //class
                                        className.ToClassDeclaration()
                                            .AddModifiers(SyntaxKind.PartialKeyword)
                                            .AddBaseListTypes(IClaimSerializable)
                                            .AddMembers(
                                                $$"""
                                                  public {{IEnumerable}}<{{Claim}}> {{nameof(ClaimSerialization.IClaimSerializable.GetClaims)}}()
                                                  {
                                                      {{string.Join("\n", propToClaims).Replace("\n", "\n\t\t")}}
                                                  }
                                                  """,
                                                $$"""
                                                  public void {{nameof(ClaimSerialization.IClaimSerializable.FromClaims)}}({{IEnumerable}}<{{Claim}}> claims)
                                                  {
                                                      foreach (var {{group}} in claims.GroupBy(x => x.Type))
                                                      {
                                                          switch ({{group}}.Key)
                                                          {
                                                              {{string.Join("\n", claimsToProps).Replace("\n", "\n\t\t\t\t")}}
                                                          }
                                                      }
                                                  }
                                                  """
                                            )
                                    )).NormalizeWhitespace();

                        // Add the source code to the compilation.
                        ctx.AddSource($"{className}.g.cs", codes.GetText(Encoding.UTF8));
                    }
                });
        }

        private static bool FilterAttribute(IPropertySymbol symbol, SourceProductionContext context)
        {
            var attrs = symbol.GetAttributes();

            if (!attrs.Any(a => a.AttributeClass!.ToType()
                    .Equals(typeof(ClaimIgnoreAttribute))))
                return true;

            foreach (var info in attrs.Select(attr =>
                             attr.AttributeClass!.ToType())
                         .Where(info =>
                             info.Equals(typeof(ClaimTypeAttribute))
                             || info.Equals(typeof(ClaimConverterAttribute))))
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                    nameof(CS0001),
                    "Error",
                    CS0001,
                    nameof(ClaimSerializableAttribute),
                    DiagnosticSeverity.Error,
                    true
                ), symbol.Locations.First(), info.Name, nameof(ClaimIgnoreAttribute)));
                return false;
            }

            return false;
        }

        private static (string claimType, IPropertySymbol typeSymbol, string propName, INamedTypeSymbol? converter)
            Transform(IPropertySymbol symbol, SourceProductionContext context)
        {
            var attrs = symbol.GetAttributes();
            var attr = attrs.FirstOrDefault(x =>
                x.AttributeClass!.GetFullyQualifiedName() == $"global::{typeof(ClaimTypeAttribute).FullName}");
            var claimType = $"nameof({symbol.Name})";
            if (attr is { ConstructorArguments.Length: 1 })
            {
                claimType = $"\"{attr.ConstructorArguments[0].Value}\"";
            }

            INamedTypeSymbol? converter = null;
            var convertAttr = attrs.FirstOrDefault(x =>
                x.AttributeClass!.GetFullyQualifiedName() == $"global::{typeof(ClaimConverterAttribute).FullName}");
            if (convertAttr is { ConstructorArguments.Length: 1 })
            {
                converter = convertAttr.ConstructorArguments[0].GetArgumentType()!;
                if (!converter.InstanceConstructors.Any(x => x.TypeArguments.Length == 0))
                {
                    context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                        nameof(CS0003),
                        "Error",
                        CS0003,
                        nameof(ClaimSerializableAttribute),
                        DiagnosticSeverity.Error,
                        true
                    ), symbol.Locations.First(), converter.Name));
                    converter = null;
                }
            
            }

            return (claimType, symbol, $"this.{symbol.Name}", converter);
        }

    }

    class Nest<T> : Dictionary<T, Nest<T>>
    {
    }
}


namespace Feast.CodeAnalysis.CompileTime
{
    internal partial class Type
    {
        
    }

    internal partial class Assembly
    {
        
    }
}
