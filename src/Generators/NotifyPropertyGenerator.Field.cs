using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Minimal.Mvvm.SourceGenerator;

partial struct NotifyPropertyGenerator
{
    #region Methods

    private static bool IsValidVariableDeclarator(VariableDeclaratorSyntax variableDeclaratorSyntax)
    {
        if (variableDeclaratorSyntax is not
        {
            Parent: VariableDeclarationSyntax
            {
                Parent: FieldDeclarationSyntax
                {
                    AttributeLists.Count: > 0,
                    Parent: ClassDeclarationSyntax
                } fieldDeclarationSyntax
            }
        })
        {
            return false;
        }

        if (fieldDeclarationSyntax.Modifiers.Any(SyntaxKind.StaticKeyword) 
            || fieldDeclarationSyntax.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
            )
        {
            return false;
        }

        return true;
    }

    internal static bool IsValidField(Compilation compilation, IFieldSymbol fieldSymbol)
    {
        return !fieldSymbol.IsReadOnly && IsValidContainingType(compilation, fieldSymbol.ContainingType);
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
            propertyName, backingFieldName, generateBackingFieldName: false);

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
        propertyName = char.ToUpper(propertyName[0]) + propertyName.Substring(1);
        return propertyName;
    }

    #endregion
}
