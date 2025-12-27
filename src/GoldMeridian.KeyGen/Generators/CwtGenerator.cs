using System.Text;
using GoldMeridian.KeyGen.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace GoldMeridian.KeyGen;

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
                        using System;
                        using Microsoft.CodeAnalysis;
                        
                        namespace GoldMeridian.CodeAnalysis;
                        
                        [Embedded]
                        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
                        internal sealed class ExtensionDataForAttribute(string? name = null) : Attribute
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
