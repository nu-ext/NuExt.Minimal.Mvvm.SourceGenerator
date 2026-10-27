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
        Command,
        NotifyDataErrorInfo,
        Localize,
        GenerateCommands
    }

    #region Pipelines

    private static readonly (string fullyQualifiedMetadataName,
        Func<SyntaxNode, CancellationToken, bool> predicate,
        Func<GeneratorAttributeSyntaxContext, CancellationToken, (ISymbol member, ImmutableArray<AttributeData> attributes, AttributeType attributeType)> transform)[] s_pipelines =
    [
        //Notify
        (fullyQualifiedMetadataName: NotifyPropertyGenerator.NotifyAttributeFullyQualifiedMetadataName,
            predicate: NotifyPropertyGenerator.IsValidSyntaxNode,
            transform: static (context, _) => (member: context.TargetSymbol, attributes: context.Attributes, AttributeType.Notify)),
        //Command
        (fullyQualifiedMetadataName: NotifyPropertyGenerator.CommandAttributeFullyQualifiedMetadataName,
            predicate: NotifyPropertyGenerator.IsValidCommandSyntaxNode,
            transform: static (context, _) => (member: context.TargetSymbol, attributes: context.Attributes, AttributeType.Command)),
        //NotifyDataErrorInfo
        (fullyQualifiedMetadataName: NotifyDataErrorInfoGenerator.NotifyDataErrorInfoAttributeFullyQualifiedMetadataName,
            predicate: NotifyDataErrorInfoGenerator.IsValidSyntaxNode,
            transform: static (context, _) => (member: context.TargetSymbol, attributes: context.Attributes, AttributeType.NotifyDataErrorInfo)),
        //Localize
        (fullyQualifiedMetadataName: LocalizePropertyGenerator.LocalizeAttributeFullyQualifiedMetadataName,
            predicate: LocalizePropertyGenerator.IsValidSyntaxNode,
            transform: static (context, _) => (member: context.TargetSymbol, attributes: context.Attributes, AttributeType.Localize)),
        //GenerateCommands
        (fullyQualifiedMetadataName: CommandGenerator.GenerateCommandsAttributeFullyQualifiedMetadataName,
            predicate: CommandGenerator.IsValidSyntaxNode,
            transform: static (context, _) => (member: context.TargetSymbol, attributes: context.Attributes, AttributeType.GenerateCommands)),
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
            .Where(static (text) => text.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
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
                                if (!NotifyPropertyGenerator.IsValidContainingType(compilation, fieldSymbol.ContainingType))
                                {
                                    // first, report and skip if containing type is not valid
                                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidContainingType,
                                        symbol.Locations.FirstOrDefault(), symbol.ContainingType.Name, symbol.Name, attributeType));
                                    continue;
                                }
                                if (!NotifyPropertyGenerator.IsValidFieldSignature(compilation, fieldSymbol, attributes))
                                {
                                    // second, report and skip if field is not valid
                                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidNotifyFieldSignature,
                                        fieldSymbol.Locations.FirstOrDefault(), fieldSymbol.Name));
                                    continue;
                                }
                                break;

                            case IPropertySymbol propertySymbol:
                                if (!NotifyPropertyGenerator.IsValidContainingType(compilation, propertySymbol.ContainingType))
                                {
                                    // first, report and skip if containing type is not valid
                                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidContainingType,
                                        symbol.Locations.FirstOrDefault(), symbol.ContainingType.Name, symbol.Name, attributeType));
                                    continue;
                                }
                                if (!NotifyPropertyGenerator.IsValidPropertySignature(compilation, propertySymbol))
                                {
                                    // second, report and skip if property is not valid
                                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidNotifyPropertySignature,
                                        propertySymbol.Locations.FirstOrDefault(), propertySymbol.Name));
                                    continue;
                                }
                                break;

                            case IMethodSymbol methodSymbol:
                                if (NotifyPropertyGenerator.HasCommandAttribute(methodSymbol))
                                {
                                    // first, skip if method has [Command]
                                    continue;
                                }
                                if (!NotifyPropertyGenerator.IsValidContainingType(compilation, methodSymbol.ContainingType))
                                {
                                    // second, report and skip if containing type is not valid
                                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidContainingType,
                                        symbol.Locations.FirstOrDefault(), symbol.ContainingType.Name, symbol.Name, attributeType));
                                    continue;
                                }
                                if (!NotifyPropertyGenerator.IsValidCommandMethodSignature(compilation, methodSymbol))
                                {
                                    // third, report and skip if method is not valid
                                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidNotifyMethodSignature,
                                        methodSymbol.Locations.FirstOrDefault(), methodSymbol.Name));
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

                    case AttributeType.Command:
                        if (symbol is not IMethodSymbol commandMethodSymbol)
                        {
                            continue;
                        }

                        if (NotifyPropertyGenerator.HasNotifyAttribute(commandMethodSymbol))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ConflictingCommandAttributes,
                                commandMethodSymbol.Locations.FirstOrDefault(), commandMethodSymbol.Name));
                            continue;
                        }

                        if (!NotifyPropertyGenerator.IsValidContainingType(compilation, commandMethodSymbol.ContainingType))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidContainingType,
                                symbol.Locations.FirstOrDefault(), symbol.ContainingType.Name, symbol.Name, attributeType));
                            continue;
                        }

                        if (!NotifyPropertyGenerator.IsValidCommandMethodSignature(compilation, commandMethodSymbol))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidCommandMethodSignature,
                                commandMethodSymbol.Locations.FirstOrDefault(), commandMethodSymbol.Name));

                            continue;
                        }

                        if (!typeInfos.TryGetValue(commandMethodSymbol.ContainingType, out typeInfo))
                        {
                            typeInfos[commandMethodSymbol.ContainingType] = typeInfo = [];
                        }
                        typeInfo.Add(item);
                        break;

                    case AttributeType.NotifyDataErrorInfo:
                        if (symbol is not INamedTypeSymbol namedTypeSymbol)
                        {
                            continue;
                        }

                        if (!NotifyPropertyGenerator.IsValidContainingType(compilation, namedTypeSymbol))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidNotifyDataErrorInfoContainingType,
                                namedTypeSymbol.Locations.FirstOrDefault(), namedTypeSymbol.Name));
                            continue;
                        }

                        if (!NotifyDataErrorInfoGenerator.IsValidContainingType(compilation, namedTypeSymbol))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.MissingNotifyDataErrorInfoImplementation,
                                namedTypeSymbol.Locations.FirstOrDefault(), namedTypeSymbol.Name));
                            continue;
                        }

                        if (!typeInfos.TryGetValue(namedTypeSymbol, out typeInfo))
                        {
                            typeInfos[namedTypeSymbol] = typeInfo = [];
                        }
                        typeInfo.Add(item);
                        break;

                    case AttributeType.Localize:
                        if (symbol is not INamedTypeSymbol typeSymbol)
                        {
                            continue;
                        }

                        if (!LocalizePropertyGenerator.IsValidConfiguration(attributes, additionalTexts))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidLocalizeConfiguration,
                                typeSymbol.Locations.FirstOrDefault(), typeSymbol.Name));
                            continue;
                        }

                        if (!LocalizePropertyGenerator.TryValidateJson(attributes, additionalTexts, out var jsonFileName, out var error))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidLocalizationFile,
                                typeSymbol.Locations.FirstOrDefault(), jsonFileName, typeSymbol.Name, error));
                            continue;
                        }

                        if (!typeInfos.TryGetValue(typeSymbol, out typeInfo))
                        {
                            typeInfos[typeSymbol] = typeInfo = [];
                        }
                        typeInfo.Add(item);
                        break;

                    case AttributeType.GenerateCommands:
                        if (symbol is not INamedTypeSymbol commandTypeSymbol)
                        {
                            continue;
                        }

                        if (!CommandGenerator.IsValidContainingType(compilation, commandTypeSymbol))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidGenerateCommandsType,
                                commandTypeSymbol.Locations.FirstOrDefault(), commandTypeSymbol.Name));
                            continue;
                        }

                        bool hasDeclaredInfrastructureMethods = false;
                        foreach (var declaredInfrastructureMethod in CommandGenerator.GetDeclaredInfrastructureMethods(compilation, commandTypeSymbol))
                        {
                            hasDeclaredInfrastructureMethods = true;
                            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.CommandInfrastructureMethodDeclared,
                                declaredInfrastructureMethod.Locations.FirstOrDefault(), commandTypeSymbol.Name, declaredInfrastructureMethod.Name));
                        }
                        if (hasDeclaredInfrastructureMethods)
                        {
                            continue;
                        }

                        if (!typeInfos.TryGetValue(commandTypeSymbol, out typeInfo))
                        {
                            typeInfos[commandTypeSymbol] = typeInfo = [];
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
                foreach (var member in containingType.GetMembers())
                {
                    backingFieldNameCache.Add(member.Name);
                }

                bool generateCommands = members.Any(static item => item.attributeType == AttributeType.GenerateCommands);
                var commandInfos = new List<CommandInfo>();
                bool hasInvalidCommandInfo = false;

                var generatedProperties = members
                    .Where(static item => item.attributeType is AttributeType.Notify or AttributeType.Command)
                    .Select(static item => (item.member, propertyName: NotifyPropertyGenerator.GetGeneratedPropertyName(item.member)))
                    .Where(static item => item.propertyName is not null)
                    .Select(static item => (item.member, propertyName: item.propertyName!))
                    .ToList();

                var invalidGeneratedMembers = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

                foreach (var group in generatedProperties
                             .GroupBy(static item => item.propertyName, StringComparer.Ordinal)
                             .Where(static group => group.Skip(1).Any()))
                {
                    foreach (var item in group)
                    {
                        invalidGeneratedMembers.Add(item.member);

                        if (generateCommands && item.member is IMethodSymbol)
                        {
                            hasInvalidCommandInfo = true;
                        }

                        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.DuplicateGeneratedPropertyName,
                            item.member.Locations.FirstOrDefault(), containingType.Name, item.propertyName));
                    }
                }

                foreach (var item in generatedProperties)
                {
                    if (item.member is IPropertySymbol || containingType.GetMembers(item.propertyName).Length == 0)
                    {
                        continue;
                    }

                    invalidGeneratedMembers.Add(item.member);

                    if (generateCommands && item.member is IMethodSymbol)
                    {
                        hasInvalidCommandInfo = true;
                    }

                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.GeneratedPropertyAlreadyDeclared,
                        item.member.Locations.FirstOrDefault(), containingType.Name, item.propertyName, item.member.Name));
                }

                if (generateCommands)
                {
                    foreach (var commandMethod in members
                                 .Where(static item => item is
                                 {
                                     member: IMethodSymbol,
                                     attributeType: AttributeType.Notify or AttributeType.Command
                                 })
                                 .Select(static item => (IMethodSymbol)item.member)
                                 .Where(method => !invalidGeneratedMembers.Contains(method)))
                    {
                        if (CommandGenerator.TryGetCommandInfo(compilation, commandMethod, out var commandInfo, out var canExecuteResolution, out var canExecuteName))
                        {
                            commandInfos.Add(commandInfo);
                            continue;
                        }

                        hasInvalidCommandInfo = true;

                        if (canExecuteResolution == CanExecuteResolution.NotFound)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.CommandCanExecuteMethodNotFound,
                                commandMethod.Locations.FirstOrDefault(), commandMethod.Name, canExecuteName));
                        }
                        else if (canExecuteResolution == CanExecuteResolution.InvalidSignature)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidCommandCanExecuteMethodSignature,
                                commandMethod.Locations.FirstOrDefault(), commandMethod.Name, canExecuteName));
                        }
                        else
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.CommandTypeNotFound,
                                commandMethod.Locations.FirstOrDefault(), commandMethod.Name));
                        }
                    }

                    foreach (var group in commandInfos.GroupBy(static commandInfo => commandInfo.ExecuteMethod.Name).Where(static group => group.Skip(1).Any()))
                    {
                        hasInvalidCommandInfo = true;

                        foreach (var commandInfo in group)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.DuplicateCommandMethodName,
                                commandInfo.ExecuteMethod.Locations.FirstOrDefault(), containingType.Name, commandInfo.ExecuteMethod.Name));
                        }
                    }

                } // generateCommands

                bool isFirst = true;
                foreach (var group in members.GroupBy(m => m.attributeType))
                {
                    switch (group.Key)
                    {
                        case AttributeType.Notify:
                        case AttributeType.Command:
                            NotifyPropertyGenerator.Generate(genCtx, new NotifyPropertyGeneratorContext(writer,
                                    group.Select(static item => item.member).Where(member => !invalidGeneratedMembers.Contains(member)),
                                    containingType.Name + '.', backingFieldNameCache), ref isFirst);
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

                if (generateCommands && !hasInvalidCommandInfo)
                {
                    CommandGenerator.Generate(writer, commandInfos, ref isFirst);
                }

                for (int i = 0; i < outerTypes.Count; i++)
                {
                    writer.Indent--;
                    writer.WriteLine('}');
                }

                writer.WriteSourceNamespaceEnd(containingNamespace);
                var sourceText = sb.ToString();

                // generated file name: <namespace>.<type>.g.cs
                sb.Clear();

                if (containingNamespace is not null)
                {
                    sb.Append(containingNamespace);
                    sb.Append('.');
                }

                for (int i = outerTypes.Count - 1; i >= 0; i--)
                {
                    var type = containingType;

                    for (int j = 0; j < i; j++)
                    {
                        type = type.ContainingType!;
                    }

                    sb.Append(type.MetadataName);

                    if (i > 0)
                    {
                        sb.Append('.');
                    }
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
                if (!(spaceCount % 4 == 0))
                {
                    System.Diagnostics.Trace.WriteLine($"Warning: Line {i + 1} has an indent that is not a multiple of 4 spaces: {lines[i]}");
                }
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
