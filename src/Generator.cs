using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/* Useful links
 * https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md
 * https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md
 */

namespace Minimal.Mvvm.SourceGenerator;

[Generator(LanguageNames.CSharp)]
public partial class Generator : IIncrementalGenerator
{
    private enum AttributeType
    {
        Notify,
        NotifyDataErrorInfo,
        Localize
    }

    #region Pipelines

    private static readonly (string fullyQualifiedMetadataName,
        Func<SyntaxNode, CancellationToken, bool> predicate,
        Func<GeneratorAttributeSyntaxContext, CancellationToken, (ISymbol member, ImmutableArray<AttributeData> attributes, AttributeType attributeType)> transform)[] s_pipelines =
    [
        (fullyQualifiedMetadataName: NotifyPropertyGenerator.NotifyAttributeFullyQualifiedMetadataName,
            predicate: NotifyPropertyGenerator.IsValidSyntaxNode,
            transform: static (context, _) => (member: context.TargetSymbol, attributes: context.Attributes, AttributeType.Notify)),
        (fullyQualifiedMetadataName: NotifyDataErrorInfoGenerator.NotifyDataErrorInfoAttributeFullyQualifiedMetadataName,
            predicate: NotifyDataErrorInfoGenerator.IsValidSyntaxNode,
            transform: static (context, _) => (member: context.TargetSymbol, attributes: context.Attributes, AttributeType.NotifyDataErrorInfo)),
        (fullyQualifiedMetadataName: LocalizePropertyGenerator.LocalizeAttributeFullyQualifiedMetadataName,
            predicate: LocalizePropertyGenerator.IsValidSyntaxNode,
            transform: static (context, _) => (member: context.TargetSymbol, attributes: context.Attributes, AttributeType.Localize))
    ];

    #endregion

    #region Methods

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if DEBUG
        if (!System.Diagnostics.Debugger.IsAttached)
        {
            //System.Diagnostics.Debugger.Launch();
        }
#endif

        context.RegisterPostInitializationOutput(static postInitializationContext =>
        {
            foreach ((string hintName, string source) in Sources)
            {
                postInitializationContext.AddSource(hintName, SourceText.From(source, Encoding.UTF8, SourceHashAlgorithm.Sha256));
            }
        });

        var pipelines = s_pipelines.Select(pipeline =>
        {
            var (fullyQualifiedMetadataName, predicate, transform) = pipeline;
            return context.SyntaxProvider.ForAttributeWithMetadataName(
                    fullyQualifiedMetadataName: fullyQualifiedMetadataName, predicate: predicate,
                    transform: transform);
        }).ToList();

        var pipeline = context.AdditionalTextsProvider
            .Where(static (text) => text.Path.EndsWith(".json"))
            .Select(static (text, cancellationToken) =>
            {
                var name = Path.GetFileName(text.Path);
                return (name, text);
            });

        var combined = context.CompilationProvider.Combine(pipeline.Collect()).Combine(pipelines.MergeSources());

        context.RegisterSourceOutput(combined, static (context, pair) =>
        {
            var ((compilation, additionalTexts), items) = pair;

            var nullableContextOptions = compilation.Options.NullableContextOptions;
            var typeInfos = new Dictionary<INamedTypeSymbol, List<(ISymbol member, ImmutableArray<AttributeData> attributes, AttributeType attributeType)>>(SymbolEqualityComparer.Default);
            foreach (var item in items)
            {
                var (symbol, attributes, attributeType) = item;
                switch (attributeType)
                {
                    case AttributeType.Notify:
                        switch (symbol)
                        {
                            case IFieldSymbol fieldSymbol:
                                if (!NotifyPropertyGenerator.IsValidField(compilation, fieldSymbol))
                                {
                                    continue;
                                }
                                break;
                            case IPropertySymbol propertySymbol:
                                if (!NotifyPropertyGenerator.IsValidProperty(compilation, propertySymbol))
                                {
                                    continue;
                                }
                                break;
                            case IMethodSymbol methodSymbol:
                                if (!NotifyPropertyGenerator.IsValidMethod(compilation, methodSymbol))
                                {
                                    continue;
                                }
                                break;
                            default:
                                continue;
                        }
                        if (!typeInfos.TryGetValue(symbol.ContainingType, out var typeInfo))
                        {
                            typeInfos[symbol.ContainingType] = typeInfo = [];
                        }
                        typeInfo.Add(item);
                        break;
                    case AttributeType.NotifyDataErrorInfo:
                        if (symbol is not INamedTypeSymbol namedTypeSymbol || !NotifyDataErrorInfoGenerator.IsValidType(compilation, namedTypeSymbol))
                        {
                            continue;
                        }
                        if (!typeInfos.TryGetValue(namedTypeSymbol, out typeInfo))
                        {
                            typeInfos[namedTypeSymbol] = typeInfo = [];
                        }
                        typeInfo.Add(item);
                        break;
                    case AttributeType.Localize:
                        if (symbol is not INamedTypeSymbol typeSymbol || !LocalizePropertyGenerator.IsValidType(typeSymbol, attributes, additionalTexts))
                        {
                            continue;
                        }
                        if (!typeInfos.TryGetValue(typeSymbol, out typeInfo))
                        {
                            typeInfos[typeSymbol] = typeInfo = [];
                        }
                        typeInfo.Add(item);
                        break;
                }
            }
            if (typeInfos.Count == 0)
            {
                return;
            }

            var sb = new StringBuilder(2048);
            var outerTypes = new List<string>(4);
            var genCtx = new GeneratorContext(compilation, new HashSet<string>(StringComparer.Ordinal), new List<string>(4), new List<string>(4), true);
            var backingFieldNameCache = new HashSet<string>(StringComparer.Ordinal);
            foreach (var typeInfo in typeInfos)
            {
                var containingType = typeInfo.Key;
                var members = typeInfo.Value;
                string? containingNamespace = null;
                if (containingType.ContainingNamespace is { IsGlobalNamespace: false } @namespace)
                {
                    containingNamespace = @namespace.ToDisplayString(SymbolDisplayFormats.Namespace);
                }

                sb.Clear();
                using var writer = new IndentedTextWriter(new StringWriter(sb));
                writer.WriteSourceHeader(nullableContextOptions);
                writer.WriteNamespaceStart(containingNamespace);

                outerTypes.Clear();
                for (var outerType = containingType; outerType != null; outerType = outerType.ContainingType)
                {
                    outerTypes.Add(outerType.ToDisplayString(SymbolDisplayFormats.TypeDeclaration));
                }
                for (int i = outerTypes.Count - 1; i >= 0; i--)
                {
                    var outerType = outerTypes[i];
                    writer.WriteLine($"partial {outerType}");
                    writer.WriteLine('{');
                    writer.Indent++;
                }

                backingFieldNameCache.Clear();
                bool isFirst = true;
                foreach (var group in members.GroupBy(m => m.attributeType))
                {
                    switch (group.Key)
                    {
                        case AttributeType.Notify:
                            NotifyPropertyGenerator.Generate(genCtx, new NotifyPropertyGeneratorContext(writer, group.Select(m => m.member), containingType.Name + '.', backingFieldNameCache), ref isFirst);
                            break;

                        case AttributeType.NotifyDataErrorInfo:
                            NotifyDataErrorInfoGenerator.Generate(genCtx, new NotifyDataErrorInfoGeneratorContext(writer, group.Select(m => m.member)), ref isFirst);
                            break;

                        case AttributeType.Localize:
                            LocalizePropertyGenerator.Generate(new LocalizePropertyGeneratorContext(writer, group.Select(m => (m.member, m.attributes)), additionalTexts), ref isFirst);
                            break;

                        default:
                            break;
                    }
                }

                for (int i = 0; i < outerTypes.Count; i++)
                {
                    writer.Indent--;
                    writer.WriteLine('}');
                }

                writer.WriteSourceNamespaceEnd(containingNamespace);
                var sourceText = sb.ToString();

                sb.Clear();
                sb.Append(containingType.ToDisplayString(SymbolDisplayFormats.GeneratedFileName));
                if (containingType.Arity > 0)
                {
                    sb.Append('`');
                    sb.Append(containingType.Arity);
                }
                sb.Append(".g.cs");
                var generatedFileName = sb.ToString();

                context.AddSource(generatedFileName, SourceText.From(sourceText, Encoding.UTF8, SourceHashAlgorithm.Sha256));
            }// foreach (var pair in typeInfos)

            if (genCtx.CachedPropertyNames.Count > 0)
            {
                sb.Clear();
                using var writer = new IndentedTextWriter(new StringWriter(sb));
                writer.WriteSourceHeader(nullableContextOptions);
                writer.WriteNamespaceStart(EventArgsCacheGenerator.GeneratedNamespace);

                var properties = genCtx.CachedPropertyNames.ToList();
                properties.Sort();
                EventArgsCacheGenerator.Generate(new EventArgsCacheGeneratorContext(writer, properties));

                writer.WriteSourceNamespaceEnd(EventArgsCacheGenerator.GeneratedNamespace);
                var sourceText = sb.ToString();

                context.AddSource($"{EventArgsCacheGenerator.GeneratedNamespace}.{EventArgsCacheGenerator.GeneratedClassName}.g.cs",
                    SourceText.From(sourceText, Encoding.UTF8, SourceHashAlgorithm.Sha256));
            }

            if (genCtx.NotifyDataErrorTypeNames.Count > 0)
            {
                sb.Clear();
                using var writer = new IndentedTextWriter(new StringWriter(sb));
                writer.WriteSourceHeader(nullableContextOptions);
                writer.WriteNamespaceStart(DataErrorsChangedEventArgsCacheGenerator.GeneratedNamespace);

                var typeNames = genCtx.NotifyDataErrorTypeNames;
                typeNames.Sort();
                DataErrorsChangedEventArgsCacheGenerator.Generate(genCtx, new DataErrorsChangedEventArgsCacheGeneratorContext(writer, typeNames));

                writer.WriteSourceNamespaceEnd(DataErrorsChangedEventArgsCacheGenerator.GeneratedNamespace);
                var sourceText = sb.ToString();

                context.AddSource($"{DataErrorsChangedEventArgsCacheGenerator.GeneratedNamespace}.{DataErrorsChangedEventArgsCacheGenerator.GeneratedClassName}.g.cs",
                    SourceText.From(sourceText, Encoding.UTF8, SourceHashAlgorithm.Sha256));
            }

            if (genCtx.CommandManagerPropertyNames.Count > 0)
            {
                sb.Clear();
                using var writer = new IndentedTextWriter(new StringWriter(sb));
                writer.WriteSourceHeader(nullableContextOptions);
                writer.InnerWriter.Write(RequerySuggestedEventManagerSource.source);
                var sourceText = sb.ToString();

                context.AddSource(RequerySuggestedEventManagerSource.hintName,
                    SourceText.From(sourceText, Encoding.UTF8, SourceHashAlgorithm.Sha256));
            }
        });
    }

    #endregion

    #region Helpers


    private static readonly string[] s_newLineSeparators = ["\r\n", "\n"];

    internal static List<(int indent, int length, string line)> GetSourceLines(string source)
    {
        var lines = source.Split(s_newLineSeparators, StringSplitOptions.None);
        var (leadingWhitespace, leadingWhitespaceLength) = TextUtils.GetLeadingWhitespace(lines[0]);

        var list = new List<(int indent, int length, string line)>();
        for (int i = 0; i < lines.Length; i++)
        {
            if (leadingWhitespaceLength > 0 && lines[i].StartsWith(leadingWhitespace))
            {
                lines[i] = lines[i].Substring(leadingWhitespaceLength);
            }
            int indent = 0;
            int length = 0;
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                var spaceCount = TextUtils.GetSpaceCount(lines[i]);
#if DEBUG
                System.Diagnostics.Debug.Assert(spaceCount % 4 == 0);
#endif
                indent = spaceCount / 4;
                lines[i] = lines[i].Trim();
                length = lines[i].Length;
            }
            list.Add((indent, length, lines[i]));
        }
        return list;
    }

    internal static bool IsNoTabsLine(string line)
    {
        return line.StartsWith("#if") || line.StartsWith("elif") || line.StartsWith("#else") || line.StartsWith("#endif");
    }

    internal readonly ref struct GeneratorContext(Compilation compilation, HashSet<string> cachedPropertyNames,
        List<string> commandManagerPropertyNames, List<string> notifyDataErrorTypeNames, bool useEventArgsCache)
    {
        internal Compilation Compilation => compilation;
        internal HashSet<string> CachedPropertyNames => cachedPropertyNames;
        internal List<string> CommandManagerPropertyNames => commandManagerPropertyNames;
        internal List<string> NotifyDataErrorTypeNames => notifyDataErrorTypeNames;

        internal readonly bool UseEventArgsCache = useEventArgsCache;
    }

    #endregion
}
