using Microsoft.CodeAnalysis;

namespace Minimal.Mvvm.SourceGenerator;

partial struct NotifyPropertyGenerator
{
    #region Methods

    internal static bool IsValidCommandMethodSignature(Compilation compilation, IMethodSymbol methodSymbol)
    {
        if (methodSymbol.Arity != 0 ||
            methodSymbol.ExplicitInterfaceImplementations.Length != 0 ||
            methodSymbol is { IsPartialDefinition: true, PartialImplementationPart: null })
        {
            return false;
        }

        if (methodSymbol.Parameters.Length > 2 || methodSymbol.Parameters.Any(static parameter => parameter.RefKind != RefKind.None))
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

    internal static bool HasCancellableParameter(IMethodSymbol methodSymbol)
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
        var backingFieldName = ctx.GetOrAddBackingFieldName("_" + char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1));

        var propertyType = GetCommandTypeName(genCtx.Compilation, methodSymbol);

        var callbackData = GetCallbackData(genCtx.Compilation, methodSymbol.ContainingType, propertyType, notifyAttributeData);

        string nullable = genCtx.Compilation.Options.NullableContextOptions.HasFlag(NullableContextOptions.Annotations) ? "?" : "";
        var fullyQualifiedTypeName = $"{propertyType?.ToDisplayString(SymbolDisplayFormats.FullyQualifiedTypeName)}{nullable}";

        var propCtx = new NotifyPropertyContext(notifyAttributeData, callbackData, customAttributeData, alsoNotifyAttributeData,
            useCommandManagerAttributeData, isCommand: true, 
            methodSymbol.GetComment(), fullyQualifiedTypeName, isPartial: false, isInitOnly: false,
            propertyName, backingFieldName, generateBackingFieldName: true, initializeStringBackingField: false);

        GenerateProperty(genCtx, ctx, propCtx, ref isFirst);
    }

    internal static INamedTypeSymbol? GetCommandTypeName(Compilation compilation, IMethodSymbol methodSymbol)
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

    internal static string GetPropertyNameFromMethodName(string methodName)
    {
        var propertyName = methodName;
        if (propertyName.Length > "Async".Length && propertyName.EndsWith("Async", StringComparison.Ordinal))
        {
            propertyName = propertyName.Substring(0, propertyName.Length - "Async".Length);
        }
        propertyName = char.ToUpperInvariant(propertyName[0]) + propertyName.Substring(1) + "Command";
        return propertyName;
    }

    #endregion
}
