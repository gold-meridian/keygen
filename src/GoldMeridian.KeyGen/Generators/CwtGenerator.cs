using System.Text;
using GoldMeridian.KeyGen.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace GoldMeridian.KeyGen;

[Generator]
public sealed class CwtGenerator : IIncrementalGenerator
{
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
    }
}
