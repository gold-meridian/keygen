using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using GoldMeridian.KeyGen.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace GoldMeridian.KeyGen;

[Generator]
public sealed class CwtGenerator : IIncrementalGenerator
{
    private sealed record Model(
        INamedTypeSymbol DataType,
        ImmutableArray<AttributeData> Attributes
    )
    {
        public IEnumerable<Link> Links =>
            Attributes.Select(
                a => new Link(
                    (INamedTypeSymbol)a.AttributeClass!.TypeArguments[0], // TKey
                    a.ConstructorArguments.Length == 1                    // string? name
                        ? a.ConstructorArguments[0].Value as string
                        : null
                )
            );

        public string Accessibility => DataType.DeclaredAccessibility switch
        {
            Microsoft.CodeAnalysis.Accessibility.Internal => "internal",
            Microsoft.CodeAnalysis.Accessibility.Public => "public",
            _ => "internal", // We could do with a better fallback case.
        };
    }

    private readonly record struct Link(
        INamedTypeSymbol KeyType,
        string? ExplicitName
    );

    void IIncrementalGenerator.Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(
            ctx =>
            {
                ctx.PolyfillAddEmbeddedAttributeDefinition();
                ctx.AddSource(
                    "ExtensionDataForAttribute.g.cs",
                    SourceText.From(
                        """
                        #nullable enable

                        using System;
                        using Microsoft.CodeAnalysis;

                        namespace GoldMeridian.CodeAnalysis;

                        [Embedded]
                        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
                        internal sealed class ExtensionDataForAttribute<TKey>(string? name = null) : Attribute
                            where TKey : class
                        {
                            public string? Name => name;
                        }
                        """,
                        Encoding.UTF8
                    )
                );
            }
        );

        var extensionCandidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            "GoldMeridian.CodeAnalysis.ExtensionDataForAttribute`1",
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, _) => GetModel(ctx)
        ).Where(static m => m is not null);

        context.RegisterSourceOutput(
            extensionCandidates.Collect(),
            static (ctx, models) =>
            {
                foreach (var model in models)
                {
                    foreach (var link in model!.Links)
                    {
                        Emit(ctx, model, link);
                    }
                }
            }
        );
    }

    private static void Emit(SourceProductionContext ctx, Model model, Link link)
    {
        var keyType = link.KeyType;
        var valueType = model.DataType;

        var keyName = keyType.Name;
        var valueName = valueType.Name;

        var propertyName = link.ExplicitName
                        ?? (valueName.StartsWith(keyName, StringComparison.Ordinal)
                               ? valueName[keyName.Length..]
                               : valueName);

        if (propertyName.Length == 0)
        {
            propertyName = valueName;
        }

        var accessibility = model.Accessibility;
        var ns = valueType.ContainingNamespace.ToDisplayString();

        var source =
            $$"""
              #nullable enable
              
              using System.Runtime.CompilerServices;
              
              namespace {{ns}};
              
              {{accessibility}} static class {{keyName}}{{propertyName}}Extensions
              {
                  private static readonly ConditionalWeakTable<{{keyType.ToDisplayString()}}, {{valueType.ToDisplayString()}}> table = [];
                  
                  extension({{keyType.ToDisplayString()}} @this)
                  {
                        public {{valueType.ToDisplayString()}}? {{propertyName}}
                        {
                            get => table.TryGetValue(@this, out var value) ? value : null;
                            set
                            {
                                if (value is null)
                                {
                                    table.Remove(@this);
                                }
                                else
                                {
                                    table.AddOrUpdate(@this, value);
                                }
                            }
                        }
                  }
              }
              """;

        ctx.AddSource(
            $"{ns}.{keyName}.{valueName}.{propertyName}.g.cs",
            SourceText.From(source, Encoding.UTF8)
        );
    }

    private static Model? GetModel(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol dataType)
        {
            return null;
        }

        var attrs = ctx.Attributes
                       .Where(x => x.AttributeClass?.Name == "ExtensionDataForAttribute")
                       .ToImmutableArray();

        return attrs.Length == 0
            ? null
            : new Model(dataType, attrs);
    }
}
