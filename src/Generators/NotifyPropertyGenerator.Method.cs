using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Minimal.Mvvm.SourceGenerator;

partial struct NotifyPropertyGenerator
{
    #region Methods

    private static bool IsValidMethodDeclaration(MethodDeclarationSyntax methodDeclarationSyntax)
    {
        if (methodDeclarationSyntax is not
            {
                Parent: ClassDeclarationSyntax,
                AttributeLists.Count: > 0,
                ParameterList.Parameters.Count: <= 2,
            })
        {
            return false;
        }

        bool hasValidReturnType;
        bool returnsVoid = false;
        string returnType;
        switch (methodDeclarationSyntax.ReturnType)
        {
            case IdentifierNameSyntax identifierNameSyntax:
                hasValidReturnType = identifierNameSyntax.Identifier.ValueText is "Task" or "ValueTask";
                break;
            case PredefinedTypeSyntax predefinedTypeSyntax:
                returnsVoid = predefinedTypeSyntax.Keyword.IsKind(SyntaxKind.VoidKeyword);
                hasValidReturnType = returnsVoid;
                break;
            case QualifiedNameSyntax qualifiedNameSyntax:
                returnType = RemoveGlobalAlias(qualifiedNameSyntax.ToString());
                hasValidReturnType = returnType is "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask";
                break;
            default:
                hasValidReturnType = false;
                break;
        }

        if (!hasValidReturnType)
        {
            return false;
        }

        var parameters = methodDeclarationSyntax.ParameterList.Parameters;

        if (returnsVoid)
        {
            return parameters.Count <= 1;
        }

        //is async here

        //Task MethodAsync([...,] CancellationToken ct)
        if (parameters is { Count: > 0 and <= 2 } && parameters[parameters.Count - 1] is { Type: { } typeNode })
        {
            switch (typeNode)
            {
                case QualifiedNameSyntax { Right: IdentifierNameSyntax identifierNameNode }:
                    if (identifierNameNode.Identifier is { ValueText: "CancellationToken" })
                    {
                        return true;
                    }
                    break;
                case IdentifierNameSyntax identifierNameSyntax:
                    if (identifierNameSyntax.Identifier is { ValueText: "CancellationToken" })
                    {
                        return true;
                    }
                    break;
                default:
                    break;
            }
        }

        return parameters.Count <= 1;
    }

    internal static bool IsValidMethod(Compilation compilation, IMethodSymbol methodSymbol)
    {
        if (methodSymbol.Parameters.Length > 2)
        {
            return false;
        }

        if (!IsValidContainingType(compilation, methodSymbol.ContainingType))
        {
            return false;
        }

        if (methodSymbol.ReturnsVoid)
        {
            return methodSymbol.Parameters.Length <= 1;
        }

        if (!IsAsyncReturnType(compilation, methodSymbol.ReturnType))
        {
            return false;
        }

        if (HasCancellableParameter(methodSymbol))
        {
            return true;
        }

        return methodSymbol.Parameters.Length <= 1;
    }

    private static bool IsAsyncReturnType(Compilation compilation, ITypeSymbol returnType)
    {
        var taskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
        var valueTaskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");
        return SymbolEqualityComparer.Default.Equals(returnType, taskType) || SymbolEqualityComparer.Default.Equals(returnType, valueTaskType);
    }

    private static bool HasCancellableParameter(IMethodSymbol methodSymbol)
    {
        if (methodSymbol.Parameters is { Length: > 0 and <= 2 } parameters && parameters[parameters.Length - 1] is { Type: { } symbolType })
        {
            if (symbolType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ==
                "global::System.Threading.CancellationToken")
            {
                return true;
            }
        }
        return false;
    }

    private static void GenerateForMethod(scoped Generator.GeneratorContext genCtx, scoped NotifyPropertyGeneratorContext ctx, IMethodSymbol methodSymbol, ref bool isFirst)
    {
        var attributes = methodSymbol.GetAttributes();

        var commandAttribute = GetCommandAttribute(attributes);
        var notifyAttribute = GetNotifyAttribute(attributes)!;
        var alsoNotifyAttributes = GetAlsoNotifyAttributes(attributes);
        var customAttributes = GetCustomAttributes(attributes);
        var useCommandManagerAttribute = GetUseCommandManagerAttribute(attributes);

        var notifyAttributeData = commandAttribute is not null ? GetCommandAttributeData(commandAttribute) : GetNotifyAttributeData(notifyAttribute!);
        var alsoNotifyAttributeData = GetAlsoNotifyAttributeData(alsoNotifyAttributes);
        var customAttributeData = GetCustomAttributeData(customAttributes);
        var useCommandManagerAttributeData = GetUseCommandManagerAttributeData(useCommandManagerAttribute);

        var propertyName = !string.IsNullOrWhiteSpace(notifyAttributeData.PropertyName) ? notifyAttributeData.PropertyName! : GetPropertyNameFromMethodName(methodSymbol.Name);
        var backingFieldName = ctx.GetOrAddBackingFieldName("_" + char.ToLower(propertyName[0]) + propertyName.Substring(1));

        var propertyType = GetCommandTypeName(genCtx.Compilation, methodSymbol);

        var callbackData = GetCallbackData(methodSymbol.ContainingType, propertyType, notifyAttributeData);

        string nullable = genCtx.Compilation.Options.NullableContextOptions.HasFlag(NullableContextOptions.Annotations) ? "?" : "";
        var fullyQualifiedTypeName = $"{propertyType?.ToDisplayString(SymbolDisplayFormats.FullyQualifiedTypeName)}{nullable}";

        var propCtx = new NotifyPropertyContext(notifyAttributeData, callbackData, customAttributeData, alsoNotifyAttributeData,
            useCommandManagerAttributeData, isCommand: true, 
            methodSymbol.GetComment(), fullyQualifiedTypeName, isPartial: false,
            propertyName, backingFieldName, generateBackingFieldName: true, initializeStringBackingField: false);

        GenerateProperty(genCtx, ctx, propCtx, ref isFirst);
    }

    private static INamedTypeSymbol? GetCommandTypeName(Compilation compilation, IMethodSymbol methodSymbol)
    {
        bool isAsync = !methodSymbol.ReturnsVoid;
        bool supportsCancellation = isAsync && HasCancellableParameter(methodSymbol);

        var parameters = methodSymbol.Parameters;
        if (parameters.Length == 0 
            || (parameters.Length == 1 && supportsCancellation))
        {
            return compilation.GetTypeByMetadataName(!isAsync ? "Minimal.Mvvm.IRelayCommand" : "Minimal.Mvvm.IAsyncCommand");
        }

        var genericCommandType = compilation.GetTypeByMetadataName(!isAsync ? "Minimal.Mvvm.IRelayCommand`1" : "Minimal.Mvvm.IAsyncCommand`1");

        if (genericCommandType != null)
        {
            var commandType = genericCommandType.Construct(parameters[0].Type);
            return commandType;
        }

        return null;
    }

    private static string GetPropertyNameFromMethodName(string methodName)
    {
        var propertyName = methodName;
        if (propertyName.Length > "Async".Length && propertyName.EndsWith("Async"))
        {
            propertyName = propertyName.Substring(0, propertyName.Length - "Async".Length);
        }
        propertyName = char.ToUpper(propertyName[0]) + propertyName.Substring(1) + "Command";
        return propertyName;
    }

    #endregion
}
