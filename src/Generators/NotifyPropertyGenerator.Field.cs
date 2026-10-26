using Microsoft.CodeAnalysis;

namespace Minimal.Mvvm.SourceGenerator;

partial struct NotifyPropertyGenerator
{
    #region Methods

    internal static bool IsValidFieldSignature(Compilation compilation, IFieldSymbol fieldSymbol)
    {
        _ = compilation;
        return fieldSymbol is { IsStatic: false, IsReadOnly: false };
    }

    private static void GenerateForField(scoped Generator.GeneratorContext genCtx, scoped NotifyPropertyGeneratorContext ctx, IFieldSymbol fieldSymbol, ref bool isFirst)
    {
        var attributes = fieldSymbol.GetAttributes();

        var notifyAttribute = GetNotifyAttribute(attributes)!;
        var alsoNotifyAttributes = GetAlsoNotifyAttributes(attributes);
        var customAttributes = GetCustomAttributes(attributes);
        var useCommandManagerAttribute = GetUseCommandManagerAttribute(attributes);

        var notifyAttributeData = GetNotifyAttributeData(notifyAttribute);
        var alsoNotifyAttributeData = GetAlsoNotifyAttributeData(alsoNotifyAttributes);
        var customAttributeData = GetCustomAttributeData(customAttributes);
        var useCommandManagerAttributeData = GetUseCommandManagerAttributeData(useCommandManagerAttribute);

        var backingFieldName = ctx.AddBackingFieldName(fieldSymbol.Name);
        var propertyName = !string.IsNullOrWhiteSpace(notifyAttributeData.PropertyName) ? notifyAttributeData.PropertyName! : GetPropertyNameFromFieldName(backingFieldName);

        var propertyType = fieldSymbol.Type;
        var callbackData = GetCallbackData(fieldSymbol.ContainingType, propertyType, notifyAttributeData);

        bool isCommand = GetIsCommand(genCtx.Compilation, propertyType);
        var fullyQualifiedTypeName = propertyType.ToDisplayString(SymbolDisplayFormats.FullyQualifiedTypeName);

        var propCtx = new NotifyPropertyContext(notifyAttributeData, callbackData, customAttributeData, alsoNotifyAttributeData,
            useCommandManagerAttributeData, isCommand, 
            fieldSymbol.GetComment(), fullyQualifiedTypeName, isPartial: false,
            propertyName, backingFieldName, generateBackingFieldName: false, initializeStringBackingField: false);

        GenerateProperty(genCtx, ctx, propCtx, ref isFirst);
    }

    private static bool GetIsCommand(Compilation compilation, ITypeSymbol? propertyType)
    {
        if (propertyType == null) return false;
        var baseTypeSymbol = compilation.GetTypeByMetadataName("Minimal.Mvvm.IRelayCommand");
        if (baseTypeSymbol != null && propertyType.IsAssignableFromType(baseTypeSymbol))
            return true;
        return false;
    }

    private static string GetPropertyNameFromFieldName(string backingFieldName)
    {
        var propertyName = backingFieldName;
        if (propertyName.StartsWith("_"))
        {
            propertyName = propertyName.TrimStart(s_trimChars);
        }

        if (propertyName.Length == 0)
        {
            return "Empty";
        }
        propertyName = char.ToUpper(propertyName[0]) + propertyName.Substring(1);
        return propertyName;
    }

    #endregion
}
