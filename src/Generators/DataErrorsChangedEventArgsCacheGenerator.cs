using Microsoft.CodeAnalysis;
using System.CodeDom.Compiler;

namespace Minimal.Mvvm.SourceGenerator;

internal readonly ref struct DataErrorsChangedEventArgsCacheGeneratorContext(IndentedTextWriter writer, List<string> typeNames)
{
    internal readonly IndentedTextWriter Writer = writer;
    internal readonly List<string> TypeNames = typeNames;
}
internal partial struct DataErrorsChangedEventArgsCacheGenerator
{
    internal const string GeneratedNamespace = "Minimal.Mvvm";
    internal const string GeneratedClassName = "DataErrorsChangedEventArgsCache";
    internal const string GeneratedClassFullyQualifiedName = $"global::{GeneratedNamespace}.{GeneratedClassName}";

    #region Methods

    public static void Generate(scoped Generator.GeneratorContext genCtx, scoped DataErrorsChangedEventArgsCacheGeneratorContext ctx)
    {
        string nullable = genCtx.Compilation.Options.NullableContextOptions.HasFlag(NullableContextOptions.Annotations) ? "?" : string.Empty;

        var code = GetCodeSource(nullable, GeneratedClassName);
        var lines = Generator.GetSourceLines(code);

        foreach (var typeName in ctx.TypeNames)
        {
            ctx.Writer.Write("// ");
            ctx.Writer.WriteLine(typeName);
        }
        var originalIndent = ctx.Writer.Indent;
        try
        {
            foreach (var (indent, length, line) in lines)
            {
                if (length == 0 || Generator.IsNoTabsLine(line))
                {
                    ctx.Writer.WriteLineNoTabs(line);
                    continue;
                }
                ctx.Writer.Indent = originalIndent + indent;
                ctx.Writer.WriteLine(line);
            }
        }
        finally
        {
            ctx.Writer.Indent = originalIndent;
        }
    }

    #endregion
}
