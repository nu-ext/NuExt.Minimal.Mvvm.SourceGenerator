using Microsoft.CodeAnalysis;

namespace Minimal.Mvvm.SourceGenerator;

internal enum CommandExecutionKind
{
    Synchronous,
    Task,
    ValueTask
}

internal enum CanExecuteResolution
{
    None,
    Resolved,
    NotFound,
    InvalidSignature
}

internal readonly record struct CommandInfo(
    IMethodSymbol ExecuteMethod,
    IMethodSymbol? CanExecuteMethod,
    string PropertyName,
    INamedTypeSymbol PropertyType,
    ITypeSymbol? ParameterType,
    CommandExecutionKind ExecutionKind,
    bool SupportsCancellation
    );