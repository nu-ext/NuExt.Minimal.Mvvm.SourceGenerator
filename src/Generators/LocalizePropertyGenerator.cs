using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Minimal.Mvvm.SourceGenerator;

internal readonly record struct LocalizationFile(string Name, LocalizationDocument Document, string? Error)
{
    internal bool IsValid => Error is null;
}
internal readonly record struct LocalizationDocument(ImmutableArray<LocalizationMember> Members);
internal abstract record LocalizationMember(string Name);
internal sealed record LocalizationValue(string Name, string Value) : LocalizationMember(Name);
internal sealed record LocalizationSection(string Name, ImmutableArray<LocalizationMember> Members) : LocalizationMember(Name);

internal readonly ref struct LocalizePropertyGeneratorContext(IndentedTextWriter writer, IEnumerable<(ISymbol member, ImmutableArray<AttributeData> attributes)> members, ImmutableArray<LocalizationFile> localizationFiles)
{
    internal readonly IndentedTextWriter Writer = writer;
    internal readonly ImmutableArray<LocalizationFile> LocalizationFiles = localizationFiles;
    internal readonly IEnumerable<(ISymbol member, ImmutableArray<AttributeData> attributes)> Members = members;
}

internal partial struct LocalizePropertyGenerator
{
    internal const string LocalizeAttributeFullyQualifiedMetadataName = "Minimal.Mvvm.LocalizeAttribute";

    private readonly record struct LocalizeAttributeData(string? JsonFileName, string? MarkupExtension);

    #region Pipeline

    internal static bool IsValidSyntaxNode(SyntaxNode attributeTarget, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        //Trace.WriteLine($"pipeline syntaxNode={attributeTarget}");
        return attributeTarget is ClassDeclarationSyntax;
    }

    public static bool IsValidConfiguration(ImmutableArray<AttributeData> attributes, ImmutableArray<LocalizationFile> localizationFiles)
    {
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
        return localizationFiles.Any(file => file.Name == jsonFileName);
    }

    internal static bool TryValidateJson(ImmutableArray<AttributeData> attributes, ImmutableArray<LocalizationFile> localizationFiles, out string? jsonFileName, out string? error)
    {
        var localizeAttribute = GetLocalizeAttribute(attributes)!;
        var localizeAttributeData = GetLocalizeAttributeData(localizeAttribute);
        jsonFileName = Path.GetFileName(localizeAttributeData.JsonFileName);
        var fileName = jsonFileName;
        var file = localizationFiles.First(file => file.Name == fileName);
        if (!file.IsValid)
        {
            error = file.Error;
            return false;
        }
        if (file.Document.Members.Any(member => StringToValidPropertyName(member.Name) == "LoadFromJsonFile"))
        {
            error = "Root localization member 'LoadFromJsonFile' conflicts with the generated localization loading method.";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(localizeAttributeData.MarkupExtension) && file.Document.Members.Any(member => StringToValidPropertyName(member.Name) == "GetLocalizedString"))
        {
            error = "Root localization member 'GetLocalizedString' conflicts with the generated markup extension lookup method.";
            return false;
        }
        error = null;
        return true;
    }

    #endregion

    internal static LocalizationFile ParseJson(string name, SourceText? text)
    {
        if (text is null)
        {
            return new LocalizationFile(name, default, "The localization file could not be read.");
        }
        try
        {
            var root = JObject.Parse(text.ToString());
            if (!TryParseMembers(root, path: null, containingSectionName: null, out var members, out var error))
            {
                return new LocalizationFile(name, default, error);
            }
            return new LocalizationFile(name, new LocalizationDocument(members), Error: null);
        }
        catch (JsonException ex)
        {
            return new LocalizationFile(name, default, ex.Message);
        }
    }

    private static bool TryParseMembers(JObject obj, string? path, string? containingSectionName, out ImmutableArray<LocalizationMember> members, out string? error)
    {
        var builder = ImmutableArray.CreateBuilder<LocalizationMember>();
        var generatedNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in obj.Properties())
        {
            var propertyPath = string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}";
            var generatedName = StringToValidPropertyName(property.Name);
            if (generatedName == containingSectionName)
            {
                members = default;
                error = $"Localization member '{propertyPath}' generates the name '{generatedName}', which conflicts with its containing section.";
                return false;
            }
            if (generatedNames.TryGetValue(generatedName, out var conflictingPath))
            {
                members = default;
                error = $"Localization members '{conflictingPath}' and '{propertyPath}' generate the same member name '{generatedName}'.";
                return false;
            }
            generatedNames.Add(generatedName, propertyPath);
            switch (property.Value.Type)
            {
                case JTokenType.String:
                    builder.Add(new LocalizationValue(property.Name, property.Value.Value<string>()!));
                    break;
                case JTokenType.Object:
                    if (!TryParseMembers((JObject)property.Value, propertyPath, generatedName, out var sectionMembers, out error))
                    {
                        members = default;
                        return false;
                    }
                    builder.Add(new LocalizationSection(property.Name, sectionMembers));
                    break;
                case JTokenType.Null:
                    members = default;
                    error = $"Localization value '{propertyPath}' cannot be null.";
                    return false;
                default:
                    members = default;
                    error = $"Localization entry '{propertyPath}' must be a string or an object.";
                    return false;
            }
        }
        members = builder.ToImmutable();
        error = null;
        return true;
    }

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

    private static void GenerateForMember(scoped LocalizePropertyGeneratorContext ctx, ITypeSymbol typeSymbol, ImmutableArray<AttributeData> attributes, ref bool isFirst)
    {
        _ = typeSymbol;
        var localizeAttribute = GetLocalizeAttribute(attributes)!;
        var localizeAttributeData = GetLocalizeAttributeData(localizeAttribute);
        var jsonFileName = Path.GetFileName(localizeAttributeData.JsonFileName);
        var document = ctx.LocalizationFiles.First(file => file.Name == jsonFileName).Document;
        GenerateMembers(ctx.Writer, document.Members, ref isFirst);
        GenerateLoadMethods(ctx.Writer, document, ref isFirst);
        if (!string.IsNullOrWhiteSpace(localizeAttributeData.MarkupExtension))
        {
            GenerateGetStringMethod(ctx.Writer, document, ref isFirst);
        }
    }

    private static void GenerateMembers(IndentedTextWriter writer, ImmutableArray<LocalizationMember> members, ref bool isFirst)
    {
        foreach (var member in members)
        {
            if (!isFirst)
            {
                writer.WriteLineNoTabs(string.Empty);
            }
            isFirst = false;
            switch (member)
            {
                case LocalizationValue value:
                    GenerateValue(writer, value);
                    break;
                case LocalizationSection section:
                    GenerateSection(writer, section);
                    break;
            }
        }
    }

    private static void GenerateValue(IndentedTextWriter writer, LocalizationValue value)
    {
        writer.WriteLine("/// <summary>");
        writer.WriteLine($"/// Looks up a localized string similar to {EscapeString(value.Value)}.");
        writer.WriteLine("/// </summary>");
        writer.WriteLine($"public static string {StringToValidPropertyName(value.Name)} {{ get; set; }} = {JsonConvert.ToString(value.Value)};");
    }

    private static void GenerateSection(IndentedTextWriter writer, LocalizationSection section)
    {
        writer.WriteLine($"public static class {StringToValidPropertyName(section.Name)}");
        writer.WriteLine('{');
        writer.Indent++;
        var isFirst = true;
        GenerateMembers(writer, section.Members, ref isFirst);
        writer.Indent--;
        writer.WriteLine('}');
    }

    private static void GenerateLoadMethods(IndentedTextWriter writer, LocalizationDocument document, ref bool isFirst)
    {
        var code = GetLoadCodeSource(GenerateLoadAssignments(document));
        var lines = Generator.GetSourceLines(code);
        if (!isFirst)
        {
            writer.WriteLineNoTabs(string.Empty);
        }
        isFirst = false;

        var originalIndent = writer.Indent;
        try
        {
            foreach (var (indent, length, line) in lines)
            {
                if (length == 0 || Generator.IsNoTabsLine(line))
                {
                    writer.WriteLineNoTabs(line);
                    continue;
                }
                writer.Indent = originalIndent + indent;
                writer.WriteLine(line);
            }
        }
        finally
        {
            writer.Indent = originalIndent;
        }
    }

    private static string GenerateLoadAssignments(LocalizationDocument document)
    {
        var builder = new StringBuilder(8192);
        GenerateLoadAssignments(builder, document.Members, path: null, expression: null);
        return builder.ToString().TrimEnd();
    }

    private static void GenerateLoadAssignments(StringBuilder builder, ImmutableArray<LocalizationMember> members, string? path, string? expression)
    {
        foreach (var member in members)
        {
            var memberPath = string.IsNullOrEmpty(path) ? member.Name : $"{path}.{member.Name}";
            var memberName = StringToValidPropertyName(member.Name);
            var memberExpression = string.IsNullOrEmpty(expression) ? memberName : $"{expression}.{memberName}";
            switch (member)
            {
                case LocalizationValue:
                    builder.Append("            case ");
                    builder.Append(JsonConvert.ToString(memberPath));
                    builder.AppendLine(":");
                    builder.Append("                ");
                    builder.Append(memberExpression);
                    builder.AppendLine(" = value;");
                    builder.AppendLine("                break;");
                    break;
                case LocalizationSection section:
                    GenerateLoadAssignments(builder, section.Members, memberPath, memberExpression);
                    break;
            }
        }
    }

    private static void GenerateGetStringMethod(IndentedTextWriter writer, LocalizationDocument document, ref bool isFirst)
    {
        var code = GetStringCodeSource(GenerateStringMappings(document));
        var lines = Generator.GetSourceLines(code);
        if (!isFirst)
        {
            writer.WriteLineNoTabs(string.Empty);
        }
        isFirst = false;

        var originalIndent = writer.Indent;
        try
        {
            foreach (var (indent, length, line) in lines)
            {
                if (length == 0 || Generator.IsNoTabsLine(line))
                {
                    writer.WriteLineNoTabs(line);
                    continue;
                }
                writer.Indent = originalIndent + indent;
                writer.WriteLine(line);
            }
        }
        finally
        {
            writer.Indent = originalIndent;
        }
    }

    private static string GenerateStringMappings(LocalizationDocument document)
    {
        var builder = new StringBuilder(8192);
        GenerateStringMappings(builder, document.Members, path: null, expression: null);
        return builder.ToString().TrimEnd();
    }

    private static void GenerateStringMappings(StringBuilder builder, ImmutableArray<LocalizationMember> members, string? path, string? expression)
    {
        foreach (var member in members)
        {
            var memberPath = string.IsNullOrEmpty(path) ? member.Name : $"{path}.{member.Name}";
            var memberName = StringToValidPropertyName(member.Name);
            var memberExpression = string.IsNullOrEmpty(expression) ? memberName : $"{expression}.{memberName}";
            switch (member)
            {
                case LocalizationValue:
                    builder.Append("        ");
                    builder.Append(JsonConvert.ToString(memberPath));
                    builder.Append(" => ");
                    builder.Append(memberExpression);
                    builder.AppendLine(",");
                    break;
                case LocalizationSection section:
                    GenerateStringMappings(builder, section.Members, memberPath, memberExpression);
                    break;
            }
        }
    }

    internal static void GenerateMarkupExtension(IndentedTextWriter writer, string markupExtensionName, string localizationTypeName)
    {
        var code = GetMarkupExtensionCodeSource(markupExtensionName, localizationTypeName);
        var lines = Generator.GetSourceLines(code);
        var originalIndent = writer.Indent;
        try
        {
            foreach (var (indent, length, line) in lines)
            {
                if (length == 0 || Generator.IsNoTabsLine(line))
                {
                    writer.WriteLineNoTabs(line);
                    continue;
                }
                writer.Indent = originalIndent + indent;
                writer.WriteLine(line);
            }
        }
        finally
        {
            writer.Indent = originalIndent;
        }
    }

    private static string EscapeString(string value)
    {
        var s = new XText(value).ToString();
        s = s
            .Replace("\r\n", @"\r\n")
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
                jsonFileName = typedConstant.Type?.SpecialType switch
                {
                    SpecialType.System_String => (string?)typedConstant.Value,
                    _ => jsonFileName
                };
            }
        }
        string? markupExtension = null;
        foreach (var namedArgument in localizeAttribute.NamedArguments)
        {
            if (namedArgument.Key == "MarkupExtension")
            {
                markupExtension = (string?)namedArgument.Value.Value;
                break;
            }
        }
        return new LocalizeAttributeData(jsonFileName, markupExtension);
    }

    internal static string? GetMarkupExtensionName(ImmutableArray<AttributeData> attributes)
    {
        var localizeAttribute = GetLocalizeAttribute(attributes);
        return localizeAttribute is null ? null : GetLocalizeAttributeData(localizeAttribute).MarkupExtension;
    }

}