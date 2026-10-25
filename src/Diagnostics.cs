using Microsoft.CodeAnalysis;

namespace Minimal.Mvvm.SourceGenerator;

internal static class Diagnostics
{
    internal static readonly DiagnosticDescriptor ConflictingCommandAttributes = new(
        id: "NUEXT001",
        title: "Conflicting command attributes",
        messageFormat: "Method '{0}' cannot be annotated with both [Notify] and [Command]",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}