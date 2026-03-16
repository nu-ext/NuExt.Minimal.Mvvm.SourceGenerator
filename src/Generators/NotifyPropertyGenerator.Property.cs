using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Minimal.Mvvm.SourceGenerator;

partial struct NotifyPropertyGenerator
{
    #region Methods

    private static bool IsValidPropertyDeclaration(PropertyDeclarationSyntax propertyDeclarationSyntax)
    {
        if (propertyDeclarationSyntax is not
            {
                Parent: ClassDeclarationSyntax,
                AttributeLists.Count: > 0,
                AccessorList.Accessors.Count: 2
            })
        {
            return false;
        }

        if (propertyDeclarationSyntax.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return false;
        }

        if (!propertyDeclarationSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return false;
        }

        return true;
    }

    internal static bool IsValidProperty(Compilation compilation, IPropertySymbol propertySymbol)
    {
        if (compilation is not CSharpCompilation csc)
        {
            return false;
        }
        if (csc.LanguageVersion < LanguageVersion.CSharp13)
        {
            return false;
        }
        if (!IsValidContainingType(compilation, propertySymbol.ContainingType))
        {
            return false;
        }
        return propertySymbol.GetMethod is not null && propertySymbol.SetMethod is not null;
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
        var backingFieldName = ctx.GetOrAddBackingFieldName("_" + char.ToLower(propertyName[0]) + propertyName.Substring(1));

        var propertyType = propertySymbol.Type;
        var callbackData = GetCallbackData(propertySymbol.ContainingType, propertyType, notifyAttributeData);

        bool isCommand = GetIsCommand(genCtx.Compilation, propertyType);
        var fullyQualifiedTypeName = propertyType.ToDisplayString(SymbolDisplayFormats.FullyQualifiedTypeName);

        var propCtx = new NotifyPropertyContext(notifyAttributeData, callbackData, customAttributeData, alsoNotifyAttributeData,
            useCommandManagerAttributeData, isCommand, 
            comment: null, fullyQualifiedTypeName, isPartial: true,
            propertyName, backingFieldName, generateBackingFieldName: true);

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
