using Microsoft.CodeAnalysis;

namespace GoldMeridian.KeyGen.Analyzers;

public static class Diagnostics
{
    public static class Categories
    {
        public const string KEYGEN = "KeyGen";
    }

    public static DiagnosticDescriptor VisibilityDowngraded { get; } = new(
        id: "KEYGEN001",
        title: "Extension visibility reduced due to type accessibility",
        messageFormat: "The generated extension for key type '{0}' is '{1}' because the key type or one of its containing types is not more accessible.",
        category: Categories.KEYGEN,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    public static DiagnosticDescriptor InaccessibleKeyType { get; } = new(
        id: "KEYGEN020",
        title: "Key type is not accessible",
        messageFormat: "Cannot generate extension members for key type '{0}' because it or one of its containing types is private or protected.",
        category: Categories.KEYGEN,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static DiagnosticDescriptor InaccessibleValueType { get; } = new(
        id: "KEYGEN021",
        title: "Extension data type is not accessible",
        messageFormat: "Cannot generate extension members because extension data type '{0}' or one of its containing types is private or protected.",
        category: Categories.KEYGEN,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static DiagnosticDescriptor PropertyNameCollision { get; } = new(
        id: "KEYGEN010",
        title: "Extension property name collision",
        messageFormat: "Multiple extension data types generate a property named '{0}' for key type '{1}'.",
        category: Categories.KEYGEN,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static DiagnosticDescriptor ConflictsWithExistingMember { get; } = new(
        id: "KEYGEN011",
        title: "Extension property conflicts with existing member",
        messageFormat: "The generated extension property '{0}' conflicts with an existing member on key type '{1}'.",
        category: Categories.KEYGEN,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );
}
