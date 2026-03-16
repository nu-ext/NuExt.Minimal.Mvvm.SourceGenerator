using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Xml.Linq;

namespace Minimal.Mvvm.SourceGenerator;

internal readonly ref struct LocalizePropertyGeneratorContext(IndentedTextWriter writer, IEnumerable<(ISymbol member, ImmutableArray<AttributeData> attributes)> members, ImmutableArray<(string name, AdditionalText text)> additionalTexts)
{
    internal readonly IndentedTextWriter Writer = writer;
    internal readonly ImmutableArray<(string name, AdditionalText text)> AdditionalTexts = additionalTexts;
    internal readonly IEnumerable<(ISymbol member, ImmutableArray<AttributeData> attributes)> Members = members;
}

internal struct LocalizePropertyGenerator
{
    internal const string LocalizeAttributeFullyQualifiedMetadataName = "Minimal.Mvvm.LocalizeAttribute";

    private readonly record struct LocalizeAttributeData(string? JsonFileName);

    #region Pipeline

    internal static bool IsValidSyntaxNode(SyntaxNode attributeTarget, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        //Trace.WriteLine($"pipeline syntaxNode={attributeTarget}");
        return attributeTarget is ClassDeclarationSyntax;
    }

    public static bool IsValidType(ITypeSymbol typeSymbol,
        ImmutableArray<AttributeData> attributes,
        ImmutableArray<(string name, AdditionalText text)> additionalTexts)
    {
        _ = typeSymbol;
        var localizeAttribute = GetLocalizeAttribute(attributes);
        if (localizeAttribute == null)
        {
            return false;
        }
        var localizeAttributeData = GetLocalizeAttributeData(localizeAttribute);
        if (string.IsNullOrEmpty(localizeAttributeData.JsonFileName))
        {
            return false;
        }
        var jsonFileName = Path.GetFileName(localizeAttributeData.JsonFileName);

        return additionalTexts.Any(pair => pair.name == jsonFileName);
    }

    #endregion

    #region Methods

    public static void Generate(scoped LocalizePropertyGeneratorContext ctx, ref bool isFirst)
    {
        foreach (var (member, attributes) in ctx.Members)
        {
            if (member is not ITypeSymbol typeSymbol)
            {
                Trace.WriteLine($"{member} is not a ITypeSymbol");
                continue;
            }
            GenerateForMember(ctx, typeSymbol, attributes, ref isFirst);
        }
    }

    private static void GenerateForMember(scoped LocalizePropertyGeneratorContext ctx, ITypeSymbol typeSymbol,
        ImmutableArray<AttributeData> attributes, ref bool isFirst)
    {
        _ = typeSymbol;
        var localizeAttribute = GetLocalizeAttribute(attributes)!;
        var localizeAttributeData = GetLocalizeAttributeData(localizeAttribute);
        var jsonFileName = Path.GetFileName(localizeAttributeData.JsonFileName);

        var text = ctx.AdditionalTexts.First(pair => pair.name == jsonFileName).text;

        Dictionary<string, string>? translations;
        try
        {
            translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(text.GetText()!.ToString());
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Exception while deserializing '{jsonFileName}': {ex.Message}");
            return;
        }

        if (translations == null || translations.Count == 0)
        {
            return;
        }

        foreach (var pair in translations)
        {
            if (!isFirst)
            {
                ctx.Writer.WriteLineNoTabs(string.Empty);
            }
            isFirst = false;
            ctx.Writer.WriteLine("/// <summary>");
            ctx.Writer.WriteLine($"/// Looks up a localized string similar to {EscapeString(pair.Value)}.");
            ctx.Writer.WriteLine("/// </summary>");
            ctx.Writer.WriteLine($"public static string {StringToValidPropertyName(pair.Key)} {{ get; set; }} = {JsonConvert.ToString(pair.Value)};");
        }
    }

    private static string EscapeString(string value)
    {
        var s = new XElement("t", value).LastNode.ToString();
        s = s
            //.Replace("\"", "&quot;")
            //.Replace("'", "&apos;")
            .Replace("\r\n", "\\r\\n")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
        return s;
    }

    private static string StringToValidPropertyName(string key)
    {
        _ = key ?? throw new ArgumentNullException(nameof(key));

        int start = 0;
        int end = key.Length - 1;

        while (start <= end && char.IsWhiteSpace(key[start])) start++;
        while (end >= start && char.IsWhiteSpace(key[end])) end--;

        int len = end - start + 1;
        if (len <= 0) return "_";

#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        return string.Create(len, (key, start), static (dest, state) =>
        {
            var src = state.key.AsSpan(state.start, dest.Length);

            char c0 = src[0];
            dest[0] = char.IsLetter(c0) ? char.ToUpperInvariant(c0) : '_';

            for (int i = 1; i < dest.Length; i++)
            {
                char ch = src[i];
                dest[i] = char.IsLetterOrDigit(ch) ? ch : '_';
            }
        });
#else
        var buf = new char[len];

        char c0 = key[start];
        buf[0] = char.IsLetter(c0) ? char.ToUpperInvariant(c0) : '_';

        int index = start + 1;
        for (int i = 1; i < len; i++)
        {
            char ch = key[index++];
            buf[i] = char.IsLetterOrDigit(ch) ? ch : '_';
        }
        return new string(buf);
#endif
    }

    private static AttributeData? GetLocalizeAttribute(ImmutableArray<AttributeData> attributes)
    {
        return attributes.SingleOrDefault(x => x.AttributeClass?.Name == "LocalizeAttribute");
    }

    private static LocalizeAttributeData GetLocalizeAttributeData(AttributeData localizeAttribute)
    {
        string? jsonFileName = null;
        if (localizeAttribute.ConstructorArguments.Length > 0)
        {
            foreach (var typedConstant in localizeAttribute.ConstructorArguments)
            {
                switch (typedConstant.Type?.SpecialType)
                {
                    case SpecialType.System_String:
                        jsonFileName = (string?)typedConstant.Value;
                        break;
                }
            }
        }
        return new LocalizeAttributeData(jsonFileName);
    }

    #endregion
}
