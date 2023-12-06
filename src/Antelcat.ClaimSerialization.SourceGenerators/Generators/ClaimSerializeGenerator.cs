using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Antelcat.ClaimSerialization.Attributes;
using Antelcat.ClaimSerialization.Interfaces;
using Feast.CodeAnalysis.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TypeInfo = Feast.CodeAnalysis.Utils.TypeInfo;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Antelcat.ClaimSerialization.SourceGenerators.Generators;

[Generator]
public class ClaimSerializeGenerator : IIncrementalGenerator
{
    private static readonly string Attribute          = $"{typeof(ClaimSerializableAttribute).FullName}";
    private static readonly string IClaimSerializable = $"global::{typeof(IClaimSerializable).FullName}";
    private const           string Claim              = "global::System.Security.Claims.Claim";
    private const           string JsonSerializer     = "global::System.Text.Json.JsonSerializer";
    private const           string IEnumerable        = "global::System.Collections.Generic.IEnumerable";

    private const string group = nameof(group);

    private static readonly IEnumerable<TypeInfo> ParsableTypes = typeof(object)
        .Assembly
        .GetExportedTypes()
        .Where(x => x.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m =>
            {
                if (m.Name != nameof(int.Parse)) return false;
                var param = m.GetParameters();
                return param.Length == 1 && param[0].ParameterType == typeof(string);
            }) != null)
        .Select(TypeInfo.FromType);


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
        if (propertyType.IsJsonBool()
            || propertyType.IsJsonNumber()
            || propertyType.IsJsonString()
            || propertyType.TypeKind == TypeKind.Enum
            || ParsableTypes.Any(x => x.SameAs(TypeInfo.FromSymbol(propertyType))))
        {
            return
                $"yield return new {Claim}({claimType}, $\"{{{propertyName}}}\", {GetClaimValueTypes(propertyType)});";
        }

        var enumerable = (RuntimeTypeInfo)typeof(IEnumerable<>);
        if (!TypeInfo.FromSymbol(propertyType).IsAssignableTo(enumerable))
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
            || ParsableTypes.Any(x => x.SameAs(TypeInfo.FromSymbol(propertyType))))
        {
            return $"{(!inner ? $"{propName} = " : string.Empty)}{
                (propertyType.TypeKind != TypeKind.Enum ? propertyType.GetFullyQualifiedName() : $"global::{typeof(Enum).FullName}")
            }.Parse{(
                propertyType.TypeKind == TypeKind.Enum ? $"<{propertyType.GetFullyQualifiedName()}>" : string.Empty)}({argumentName})";
        }

        var enumerable = (RuntimeTypeInfo)typeof(IEnumerable<>);
        if (!TypeInfo.FromSymbol(propertyType).IsAssignableTo(enumerable))
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
                (ctx, _) => (ctx.TargetNode as ClassDeclarationSyntax)!);

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(provider.Collect()),
            (ctx, t) => GenerateCode(ctx, t.Left, t.Right));
    }

    private static void GenerateCode(SourceProductionContext context, Compilation compilation,
        ImmutableArray<ClassDeclarationSyntax> classDeclarations)
    {
        // Go through all filtered class declarations.
        foreach (var classDeclarationSyntax in classDeclarations)
        {
            // We need to get semantic model of the class to retrieve metadata.
            var semanticModel = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);

            // Symbols allow us to get the compile-time information.
            if (ModelExtensions.GetDeclaredSymbol(semanticModel, classDeclarationSyntax) is not INamedTypeSymbol
                classSymbol)
                continue;

            var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();

            // 'Identifier' means the token of the node. Get class name from the syntax node.
            var className = classDeclarationSyntax.Identifier.Text;

            // Go through all class members with a particular type (property) to generate method lines.
            var transform = classSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(x => FilterAttribute(x, context))
                .Select(x => Transform(x, context))
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
                        context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
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
                                 ? GenerateClaimToProp(p.propName, $"{group}.First().Value", p.typeSymbol.Type)
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
                                      public {{IEnumerable}}<{{Claim}}> {{nameof(Interfaces.IClaimSerializable.GetClaims)}}()
                                      {
                                          {{string.Join("\n", propToClaims).Replace("\n", "\n\t\t")}}
                                      }
                                      """,
                                    $$"""
                                      public void {{nameof(Interfaces.IClaimSerializable.FromClaims)}}({{IEnumerable}}<{{Claim}}> claims)
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
            context.AddSource($"{className}.g.cs", codes.GetText(Encoding.UTF8));
        }
    }

    private static bool FilterAttribute(IPropertySymbol symbol, SourceProductionContext context)
    {
        var attrs = symbol.GetAttributes();

        if (!attrs.Any(a => TypeInfo
                .FromSymbol(a.AttributeClass!)
                .SameAs(typeof(ClaimIgnoreAttribute))))
            return true;

        foreach (var info in attrs.Select(attr =>
                         TypeInfo.FromSymbol(attr.AttributeClass!))
                     .Where(info =>
                         info.SameAs(typeof(ClaimTypeAttribute))
                         || info.SameAs(typeof(ClaimConverterAttribute))))
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
            x.AttributeClass.GetFullyQualifiedName() == $"global::{typeof(ClaimTypeAttribute).FullName}");
        var claimType = $"nameof({symbol.Name})";
        if (attr is { ConstructorArguments.Length: 1 })
        {
            claimType = $"\"{attr.ConstructorArguments[0].Value}\"";
        }

        INamedTypeSymbol? converter = null;
        var convertAttr = attrs.FirstOrDefault(x =>
            x.AttributeClass.GetFullyQualifiedName() == $"global::{typeof(ClaimConverterAttribute).FullName}");
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