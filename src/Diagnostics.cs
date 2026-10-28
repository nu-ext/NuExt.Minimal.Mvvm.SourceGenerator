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

    internal static readonly DiagnosticDescriptor CommandInfrastructureMethodDeclared = new(
        id: "NUEXT002",
        title: "Command infrastructure method is generated automatically",
        messageFormat: "Type '{0}' is annotated with [GenerateCommands] and cannot declare method '{1}'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor InvalidCommandMethodSignature = new(
        id: "NUEXT003",
        title: "Invalid command method signature",
        messageFormat: "Method '{0}' annotated with [Command] must return void, Task, or ValueTask and declare at most one command parameter and an optional trailing CancellationToken",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor InvalidNotifyMethodSignature = new(
        id: "NUEXT004",
        title: "Invalid notify method signature",
        messageFormat: "Method '{0}' annotated with [Notify] must return void, Task, or ValueTask and declare at most one command parameter and an optional trailing CancellationToken",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor InvalidNotifyFieldSignature = new(
        id: "NUEXT005",
        title: "Invalid notify field declaration",
        messageFormat: "Field '{0}' annotated with [Notify] must be an instance, non-readonly field and must produce a non-empty property name",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor InvalidNotifyPropertySignature = new(
        id: "NUEXT006",
        title: "Invalid notify property declaration",
        messageFormat: "Property '{0}' annotated with [Notify] must be a non-indexed instance partial property with both getter and setter and requires C# 13 or later",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor InvalidContainingType = new(
        id: "NUEXT007",
        title: "Invalid containing type",
        messageFormat: "Type '{0}' containing member '{1}' annotated with [{2}] must derive from BindableBase or PropertyChangeNotifier",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor InvalidNotifyDataErrorInfoContainingType = new(
        id: "NUEXT008",
        title: "Invalid containing type",
        messageFormat: "Type '{0}' annotated with [NotifyDataErrorInfo] must derive from BindableBase or PropertyChangeNotifier",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor MissingNotifyDataErrorInfoImplementation = new(
        id: "NUEXT009",
        title: "INotifyDataErrorInfo is not implemented",
        messageFormat: "Type '{0}' annotated with [NotifyDataErrorInfo] must implement INotifyDataErrorInfo",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor InvalidLocalizeConfiguration = new(
        id: "NUEXT010",
        title: "Invalid localize configuration",
        messageFormat: "Type '{0}' annotated with [Localize] must specify an existing JSON additional file",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor InvalidGenerateCommandsType = new(
        id: "NUEXT011",
        title: "Invalid generate commands type",
        messageFormat: "Type '{0}' annotated with [GenerateCommands] must derive from CommandViewModelBase",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor CommandCanExecuteMethodNotFound = new(
        id: "NUEXT012",
        title: "Command can-execute method not found",
        messageFormat: "Method '{0}' annotated with [Command] references can-execute method '{1}', but no such method exists",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor InvalidCommandCanExecuteMethodSignature = new(
        id: "NUEXT013",
        title: "Invalid command can-execute method signature",
        messageFormat: "Can-execute method '{1}' referenced by command method '{0}' must return bool and have the same command parameter",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor CommandTypeNotFound = new(
        id: "NUEXT014",
        title: "Command type not found",
        messageFormat: "Command type required for method '{0}' could not be resolved",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor DuplicateCommandMethodName = new(
        id: "NUEXT015",
        title: "Duplicate command method name",
        messageFormat: "Type '{0}' contains multiple command methods named '{1}'; [GenerateCommands] cannot distinguish overloaded command methods",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor DuplicateGeneratedPropertyName = new(
        id: "NUEXT016",
        title: "Duplicate generated property name",
        messageFormat: "Type '{0}' contains multiple members generating property '{1}'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor GeneratedPropertyAlreadyDeclared = new(
        id: "NUEXT017",
        title: "Generated property is already declared",
        messageFormat: "Type '{0}' already contains member '{1}' generated for member '{2}'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor InvalidLocalizationFile = new(
        id: "NUEXT018",
        title: "Invalid localization file",
        messageFormat: "Localization file '{0}' specified by type '{1}' contains invalid JSON: {2}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor MissingSystemTextJsonReference = new(
        id: "NUEXT019",
        title: "System.Text.Json reference is required",
        messageFormat: "Type '{0}' annotated with [Localize] requires a reference to System.Text.Json",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor MissingWpfMarkupExtensionReference = new(
        id: "NUEXT020",
        title: "WPF reference is required",
        messageFormat: "Type '{0}' annotated with [Localize] and MarkupExtension requires a reference to WindowsBase",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor InvalidMarkupExtensionName = new(
        id: "NUEXT021",
        title: "Invalid markup extension name",
        messageFormat: "MarkupExtension name '{1}' specified by type '{0}' does not produce a valid C# type name",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    internal static readonly DiagnosticDescriptor MarkupExtensionTypeAlreadyDeclared = new(
        id: "NUEXT022",
        title: "Markup extension type is already declared",
        messageFormat: "Type '{0}' cannot generate markup extension type '{1}' because it is already declared",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}