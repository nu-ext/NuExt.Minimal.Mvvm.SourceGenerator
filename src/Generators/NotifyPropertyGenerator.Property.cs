using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Minimal.Mvvm.SourceGenerator;

partial struct NotifyPropertyGenerator
{
    #region Methods

    internal static bool IsValidPropertySignature(Compilation compilation, IPropertySymbol propertySymbol)
    {
        if (compilation is not CSharpCompilation { LanguageVersion: >= LanguageVersion.CSharp13 })
        {
            return false;
        }

        if (propertySymbol.IsStatic ||
            propertySymbol.IsIndexer ||
            propertySymbol.GetMethod is null ||
            propertySymbol.SetMethod is null ||
            (propertySymbol.PartialDefinitionPart is not null || propertySymbol.PartialImplementationPart is not null))
        {
            return false;
        }

        return propertySymbol.DeclaringSyntaxReferences.Any(static syntaxReference =>
            syntaxReference.GetSyntax() is PropertyDeclarationSyntax propertyDeclaration &&
            propertyDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword));
    }

    private static void GenerateForProperty(scoped Generator.GeneratorContext genCtx, NotifyPropertyGeneratorContext ctx, IPropertySymbol propertySymbol, ref bool isFirst)
    {
        var attributes = propertySymbol.GetAttributes();

        var notifyAttribute = GetNotifyAttribute(attributes)!;
        var alsoNotifyAttributes = GetAlsoNotifyAttributes(attributes);
        var customAttributes = GetCustomAttributes(attributes);
        var useCommandManagerAttribute = GetUseCommandManagerAttribute(attributes);

        var notifyAttributeData = GetNotifyAttributeData(notifyAttribute, propertySymbol);
        var alsoNotifyAttributeData = GetAlsoNotifyAttributeData(alsoNotifyAttributes);
        var customAttributeData = GetCustomAttributeData(customAttributes);
        var useCommandManagerAttributeData = GetUseCommandManagerAttributeData(useCommandManagerAttribute);

        var propertyName = propertySymbol.Name;
        var backingFieldName = ctx.GetOrAddBackingFieldName("_" + char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1));

        var propertyType = propertySymbol.Type;
        var callbackData = GetCallbackData(genCtx.Compilation, propertySymbol.ContainingType, propertyType, notifyAttributeData);

        bool isCommand = GetIsCommand(genCtx.Compilation, propertyType);
        var fullyQualifiedTypeName = propertyType.ToDisplayString(SymbolDisplayFormats.FullyQualifiedTypeName);

        bool initializeStringBackingField =
            propertyType is { SpecialType: SpecialType.System_String, NullableAnnotation: NullableAnnotation.NotAnnotated };

        var propCtx = new NotifyPropertyContext(notifyAttributeData, callbackData, customAttributeData, alsoNotifyAttributeData,
            useCommandManagerAttributeData, isCommand, 
            comment: null, fullyQualifiedTypeName, isPartial: true, isInitOnly: propertySymbol.SetMethod!.IsInitOnly,
            propertyName, backingFieldName, generateBackingFieldName: true, initializeStringBackingField);

        GenerateProperty(genCtx, ctx, propCtx, ref isFirst);
    }

    private static NotifyAttributeData GetNotifyAttributeData(AttributeData notifyAttribute, IPropertySymbol propertySymbol)
    {
        var notifyAttributeData = GetNotifyAttributeData(notifyAttribute);

        Accessibility getterAccessibility = propertySymbol.GetMethod!.DeclaredAccessibility;
        Accessibility setterAccessibility = propertySymbol.SetMethod!.DeclaredAccessibility;

        (var propertyAccessibility, getterAccessibility, setterAccessibility) = GetAccessibility(getterAccessibility, setterAccessibility);

        if (propertySymbol.DeclaredAccessibility != propertyAccessibility)
        {
#if DEBUG
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debugger.Launch();
            }
            System.Diagnostics.Debugger.Break();
#endif
        }

        notifyAttributeData = notifyAttributeData with
        {
            PropertyAccessibility = propertyAccessibility,
            GetterAccessibility = getterAccessibility,
            SetterAccessibility = setterAccessibility
        };
        return notifyAttributeData;
    }

    #endregion
}
