using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using GoldMeridian.KeyGen.Analyzers;
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
                    a,
                    (INamedTypeSymbol)a.AttributeClass!.TypeArguments[0], // TKey
                    a.ConstructorArguments.Length == 1                    // string? name
                        ? a.ConstructorArguments[0].Value as string
                        : null
                )
            );
    }

    private readonly record struct Link(
        AttributeData Attribute,
        INamedTypeSymbol KeyType,
        string? ExplicitName
    )
    {
        public Location? GetAttributeLocation()
        {
            return Attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation();
        }
    }

    private static readonly SymbolDisplayFormat fully_qualified_minus_global = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions:
        SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
        SymbolDisplayMiscellaneousOptions.UseSpecialTypes
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
                var byKey = new Dictionary<INamedTypeSymbol, Dictionary<string, List<(Model, Link)>>>(SymbolEqualityComparer.Default);
                var duplicateModels = new HashSet<Model>();

                foreach (var model in models)
                foreach (var link in model!.Links)
                {
                    var propName = ResolvePropertyName(link.KeyType, model.DataType, link.ExplicitName);

                    if (!byKey.TryGetValue(link.KeyType, out var linksByProp))
                    {
                        byKey[link.KeyType] = linksByProp = [];
                    }

                    if (!linksByProp.TryGetValue(propName, out var links))
                    {
                        linksByProp[propName] = links = [];
                    }

                    links.Add((model, link));
                }

                foreach (var byKeyKvp in byKey)
                {
                    var keyType = byKeyKvp.Key;
                    var linksByProp = byKeyKvp.Value;

                    foreach (var propsKvp in linksByProp)
                    {
                        var name = propsKvp.Key;
                        var entries = propsKvp.Value;

                        if (entries.Count <= 1)
                        {
                            continue;
                        }

                        foreach (var (model, link) in entries)
                        {
                            ctx.ReportDiagnostic(
                                Diagnostic.Create(
                                    Diagnostics.PropertyNameCollision,
                                    link.GetAttributeLocation(),
                                    name,
                                    keyType.ToDisplayString()
                                )
                            );

                            duplicateModels.Add(model);
                        }
                    }
                }

                foreach (var model in models)
                {
                    if (duplicateModels.Contains(model!))
                    {
                        continue;
                    }
                    
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
        var keyTypeName = keyType.ToDisplayString(fully_qualified_minus_global);
        var valueTypeName = valueType.ToDisplayString(fully_qualified_minus_global);
        // var keyName = keyType.Name;
        // var valueName = valueType.Name;

        var propertyName = ResolvePropertyName(keyType, valueType, link.ExplicitName);
        if (keyType.GetMembers(propertyName).Any())
        {
            ctx.ReportDiagnostic(
                Diagnostic.Create(
                    Diagnostics.ConflictsWithExistingMember,
                    link.GetAttributeLocation(),
                    propertyName,
                    keyType.ToDisplayString()
                )
            );
        }

        var keyAcc = keyType.GetEffectiveAccessibility();
        var valueAcc = valueType.GetEffectiveAccessibility();

        if (!keyType.IsUsable())
        {
            ctx.ReportDiagnostic(
                Diagnostic.Create(
                    Diagnostics.InaccessibleKeyType,
                    link.GetAttributeLocation(),
                    keyType.ToDisplayString()
                )
            );
            return;
        }

        if (!valueType.IsUsable())
        {
            ctx.ReportDiagnostic(
                Diagnostic.Create(
                    Diagnostics.InaccessibleValueType,
                    model.DataType.Locations.FirstOrDefault(),
                    valueType.ToDisplayString()
                )
            );
            return;
        }

        var finalAcc = Accessibility.Min(keyAcc, valueAcc);
        if (finalAcc < valueAcc)
        {
            ctx.ReportDiagnostic(
                Diagnostic.Create(
                    Diagnostics.VisibilityDowngraded,
                    link.GetAttributeLocation(),
                    keyType.ToDisplayString(),
                    finalAcc.ToKeyword()
                )
            );
        }

        var accessibility = finalAcc.ToKeyword();
        var ns = valueType.ContainingNamespace.ToDisplayString();

        var cwt = $"ConditionalWeakTable<{keyType.ToDisplayString()}, {valueType.ToDisplayString()}>";
        var source =
            $$"""
              #nullable enable

              using System.Runtime.CompilerServices;

              namespace {{ns}};

              {{accessibility}} static class {{SafeTypeName(keyType)}}_{{propertyName}}_CwtExtensions
              {
                  private static readonly {{cwt}} table = [];
              
                  extension({{keyType.ToDisplayString()}} @this)
                  {
                      public static {{cwt}} Get{{propertyName}}Table()
                      {
                          return table;
                      }
                      
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
            $"{keyTypeName}.{valueTypeName}.{propertyName}.g.cs",
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

    private static string SafeTypeName(INamedTypeSymbol type)
    {
        var names = new Stack<string>();

        for (_ = 0; type is not null; type = type.ContainingType)
        {
            names.Push(type.Name);
        }

        return string.Join("_", names);
    }

    private static string ResolvePropertyName(
        INamedTypeSymbol keyType,
        INamedTypeSymbol valueType,
        string? explicitName
    )
    {
        if (!string.IsNullOrEmpty(explicitName))
        {
            return explicitName!;
        }

        var keyName = keyType.Name;
        var valueName = valueType.Name;

        if (!valueName.StartsWith(keyName, StringComparison.Ordinal))
        {
            return valueName;
        }

        var trimmed = valueName[keyName.Length..];
        return trimmed.Length > 0 ? trimmed : valueName;
    }
}
