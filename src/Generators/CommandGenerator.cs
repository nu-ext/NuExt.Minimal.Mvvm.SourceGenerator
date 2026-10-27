using System.CodeDom.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Minimal.Mvvm.SourceGenerator;

internal struct CommandGenerator
{
    internal const string GenerateCommandsAttributeFullyQualifiedMetadataName = "Minimal.Mvvm.GenerateCommandsAttribute";
    private const string CommandViewModelBaseFullyQualifiedMetadataName = "Minimal.Mvvm.Wpf.CommandViewModelBase";

    private static readonly string[] s_infrastructureMethodNames = ["CreateCommands", "GetCurrentCommand", "GetAllCommands", "NullifyCommands"];

    #region Pipeline

    internal static bool IsValidSyntaxNode(SyntaxNode attributeTarget, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return attributeTarget is ClassDeclarationSyntax;
    }

    internal static bool IsValidContainingType(Compilation compilation, INamedTypeSymbol typeSymbol)
    {
        var commandViewModelBaseType = compilation.GetTypeByMetadataName(CommandViewModelBaseFullyQualifiedMetadataName);
        return commandViewModelBaseType is not null && typeSymbol.InheritsFromType(commandViewModelBaseType);
    }

    #endregion

    #region Methods

    internal static IEnumerable<IMethodSymbol> GetDeclaredInfrastructureMethods(Compilation compilation, INamedTypeSymbol typeSymbol)
    {
        var commandViewModelBaseType = compilation.GetTypeByMetadataName(CommandViewModelBaseFullyQualifiedMetadataName);
        if (commandViewModelBaseType is null)
        {
            yield break;
        }

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is IMethodSymbol methodSymbol &&
                s_infrastructureMethodNames.Contains(methodSymbol.Name) &&
                HasInfrastructureSignature(methodSymbol, commandViewModelBaseType))
            {
                yield return methodSymbol;
            }
        }
    }

    private static bool HasInfrastructureSignature(IMethodSymbol methodSymbol, INamedTypeSymbol commandViewModelBaseType)
    {
        foreach (var member in commandViewModelBaseType.GetMembers(methodSymbol.Name))
        {
            if (member is not IMethodSymbol infrastructureMethod ||
                methodSymbol.Arity != infrastructureMethod.Arity ||
                methodSymbol.Parameters.Length != infrastructureMethod.Parameters.Length)
            {
                continue;
            }

            bool matches = true;

            for (int i = 0; i < methodSymbol.Parameters.Length; i++)
            {
                var parameter = methodSymbol.Parameters[i];
                var infrastructureParameter = infrastructureMethod.Parameters[i];

                if (parameter.RefKind != infrastructureParameter.RefKind ||
                    !SymbolEqualityComparer.Default.Equals(parameter.Type, infrastructureParameter.Type))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool TryGetCommandInfo(Compilation compilation, IMethodSymbol methodSymbol, out CommandInfo commandInfo, out CanExecuteResolution canExecuteResolution, out string? canExecuteName)
    {
        commandInfo = default;
        canExecuteResolution = CanExecuteResolution.None;
        canExecuteName = null;

        var attributes = methodSymbol.GetAttributes();
        var commandAttribute = NotifyPropertyGenerator.GetCommandAttribute(attributes);

        var propertyType = NotifyPropertyGenerator.GetCommandTypeName(compilation, methodSymbol);
        if (propertyType is null)
        {
            return false;
        }

        var propertyName = NotifyPropertyGenerator.GetCommandPropertyName(attributes);
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            propertyName = NotifyPropertyGenerator.GetPropertyNameFromMethodName(methodSymbol.Name);
        }

        IMethodSymbol? canExecuteMethod = null;
        if (commandAttribute is not null)
        {
            canExecuteResolution = TryGetCanExecuteMethod(compilation, methodSymbol, commandAttribute, out canExecuteMethod, out canExecuteName);
            if (canExecuteResolution is CanExecuteResolution.NotFound or CanExecuteResolution.InvalidSignature)
            {
                return false;
            }
        }

        bool supportsCancellation = !methodSymbol.ReturnsVoid && NotifyPropertyGenerator.HasCancellableParameter(methodSymbol);
        ITypeSymbol? parameterType = methodSymbol.Parameters.Length == 0 || (methodSymbol.Parameters.Length == 1 && supportsCancellation) ? null : methodSymbol.Parameters[0].Type;
        var executionKind = GetExecutionKind(compilation, methodSymbol);

        commandInfo = new CommandInfo(methodSymbol, canExecuteMethod, propertyName!, propertyType, parameterType, executionKind, supportsCancellation);
        return true;
    }

    private static CanExecuteResolution TryGetCanExecuteMethod(Compilation compilation, IMethodSymbol executeMethod, AttributeData commandAttribute, out IMethodSymbol? canExecuteMethod, out string? canExecuteName)
    {
        canExecuteMethod = null;

        canExecuteName = NotifyPropertyGenerator.GetCommandCanExecuteName(commandAttribute);
        bool isExplicit = !string.IsNullOrWhiteSpace(canExecuteName);
        if (!isExplicit)
        {
            var executeName = executeMethod.Name;
            if (executeName.Length > "Async".Length && executeName.EndsWith("Async", StringComparison.Ordinal))
            {
                executeName = executeName.Substring(0, executeName.Length - "Async".Length);
            }

            canExecuteName = "Can" + executeName;
        }

        bool found = false;
        for (var type = executeMethod.ContainingType; type is not null; type = type.BaseType)
        {
            foreach (var member in type.GetMembers(canExecuteName!))
            {
                if (member is not IMethodSymbol method ||
                    !compilation.IsSymbolAccessibleWithin(method, executeMethod.ContainingType))
                {
                    continue;
                }

                found = true;

                if (IsCompatibleCanExecuteMethod(compilation, executeMethod, method))
                {
                    canExecuteMethod = method;
                    return CanExecuteResolution.Resolved;
                }

                if (isExplicit && canExecuteMethod is null)
                {
                    canExecuteMethod = method;
                }
            }

            if (found)
            {
                break;
            }
        }

        if (isExplicit)
        {
            return found ? CanExecuteResolution.Resolved : CanExecuteResolution.NotFound;
        }

        return found ? CanExecuteResolution.InvalidSignature : CanExecuteResolution.None;

    }

    private static bool IsCompatibleCanExecuteMethod(Compilation compilation, IMethodSymbol executeMethod, IMethodSymbol canExecuteMethod)
    {
        _ = compilation;

        if (canExecuteMethod.Arity != 0 ||
            canExecuteMethod.ExplicitInterfaceImplementations.Length != 0 ||
            canExecuteMethod.ReturnType.SpecialType != SpecialType.System_Boolean ||
            canExecuteMethod.Parameters.Any(static parameter => parameter.RefKind != RefKind.None))
        {
            return false;
        }

        bool supportsCancellation = NotifyPropertyGenerator.HasCancellableParameter(executeMethod);
        int executeParameterCount = executeMethod.Parameters.Length - (supportsCancellation ? 1 : 0);

        if (canExecuteMethod.Parameters.Length != executeParameterCount)
        {
            return false;
        }

        return executeParameterCount == 0 || SymbolEqualityComparer.Default.Equals(executeMethod.Parameters[0].Type, canExecuteMethod.Parameters[0].Type);
    }

    private static CommandExecutionKind GetExecutionKind(Compilation compilation, IMethodSymbol methodSymbol)
    {
        if (methodSymbol.ReturnsVoid)
        {
            return CommandExecutionKind.Synchronous;
        }

        var valueTaskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");
        return SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, valueTaskType) ? CommandExecutionKind.ValueTask : CommandExecutionKind.Task;
    }

    internal static void Generate(IndentedTextWriter writer, IReadOnlyList<CommandInfo> commandInfos, ref bool isFirst)
    {
        if (commandInfos.Count == 0)
        {
            return;
        }

        GenerateCreateCommands(writer, commandInfos, ref isFirst);
        GenerateGetCurrentCommand(writer, commandInfos, ref isFirst);
        GenerateGetAllCommands(writer, commandInfos, ref isFirst);
        GenerateNullifyCommands(writer, commandInfos, ref isFirst);
    }

    private static string EscapeIdentifier(string identifier)
    {
        return SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None ? "@" + identifier : identifier;
    }

    private static void GenerateCreateCommands(IndentedTextWriter writer, IReadOnlyList<CommandInfo> commandInfos, ref bool isFirst)
    {
        WriteSeparator(writer, ref isFirst);
        writer.WriteLine("protected override void CreateCommands()");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("base.CreateCommands();");

        if (commandInfos.Count > 0)
        {
            writer.WriteLineNoTabs(string.Empty);
        }

        foreach (var commandInfo in commandInfos)
        {
            writer.Write($"{EscapeIdentifier(commandInfo.PropertyName)} = new ");

            switch (commandInfo.ExecutionKind)
            {
                case CommandExecutionKind.Synchronous:
                    writer.Write("global::Minimal.Mvvm.RelayCommand");
                    break;
                case CommandExecutionKind.Task:
                    writer.Write("global::Minimal.Mvvm.AsyncCommand");
                    break;
                case CommandExecutionKind.ValueTask:
                    writer.Write("global::Minimal.Mvvm.AsyncValueCommand");
                    break;
            }

            if (commandInfo.ParameterType is not null)
            {
                writer.Write($"<{commandInfo.ParameterType.ToDisplayString(SymbolDisplayFormats.FullyQualifiedTypeName)}>");
            }

            writer.Write($"({EscapeIdentifier(commandInfo.ExecuteMethod.Name)}");
            if (commandInfo.CanExecuteMethod is not null)
            {
                writer.Write($", {EscapeIdentifier(commandInfo.CanExecuteMethod.Name)}");
            }

            writer.WriteLine(");");
        }

        writer.Indent--;
        writer.WriteLine("}");
    }

    private static void GenerateGetCurrentCommand(IndentedTextWriter writer, IReadOnlyList<CommandInfo> commandInfos, ref bool isFirst)
    {
        WriteSeparator(writer, ref isFirst);
        writer.WriteLine("protected override global::System.Windows.Input.ICommand? GetCurrentCommand([global::System.Runtime.CompilerServices.CallerMemberName] string? callerName = null)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("return callerName switch");
        writer.WriteLine("{");
        writer.Indent++;

        foreach (var commandInfo in commandInfos)
        {
            writer.WriteLine($"nameof({EscapeIdentifier(commandInfo.ExecuteMethod.Name)}) => {EscapeIdentifier(commandInfo.PropertyName)},");
        }

        writer.WriteLine("_ => base.GetCurrentCommand(callerName)");
        writer.Indent--;
        writer.WriteLine("};");
        writer.Indent--;
        writer.WriteLine("}");
    }

    private static void GenerateGetAllCommands(IndentedTextWriter writer, IReadOnlyList<CommandInfo> commandInfos, ref bool isFirst)
    {
        WriteSeparator(writer, ref isFirst);
        writer.WriteLine("protected override void GetAllCommands(ref global::System.Collections.Generic.ValueListBuilder<(string PropertyName, global::System.Windows.Input.ICommand? Command)> builder)");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("base.GetAllCommands(ref builder);");

        if (commandInfos.Count > 0)
        {
            writer.WriteLineNoTabs(string.Empty);
        }

        foreach (var commandInfo in commandInfos)
        {
            writer.WriteLine($"builder.Append((nameof({EscapeIdentifier(commandInfo.PropertyName)}), {EscapeIdentifier(commandInfo.PropertyName)}));");
        }

        writer.Indent--;
        writer.WriteLine("}");
    }

    private static void GenerateNullifyCommands(IndentedTextWriter writer, IReadOnlyList<CommandInfo> commandInfos, ref bool isFirst)
    {
        WriteSeparator(writer, ref isFirst);
        writer.WriteLine("protected override void NullifyCommands()");
        writer.WriteLine("{");
        writer.Indent++;

        foreach (var commandInfo in commandInfos)
        {
            writer.WriteLine($"{EscapeIdentifier(commandInfo.PropertyName)} = null;");
        }

        if (commandInfos.Count > 0)
        {
            writer.WriteLineNoTabs(string.Empty);
        }

        writer.WriteLine("base.NullifyCommands();");
        writer.Indent--;
        writer.WriteLine("}");
    }

    private static void WriteSeparator(IndentedTextWriter writer, ref bool isFirst)
    {
        if (!isFirst)
        {
            writer.WriteLineNoTabs(string.Empty);
        }

        isFirst = false;
    }

    #endregion
}