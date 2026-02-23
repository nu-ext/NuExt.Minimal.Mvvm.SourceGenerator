using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Minimal.Mvvm.SourceGenerator
{
    internal readonly ref struct NotifyDataErrorInfoGeneratorContext(IndentedTextWriter writer, IEnumerable<ISymbol> members)
    {
        internal readonly IndentedTextWriter Writer = writer;
        internal readonly IEnumerable<ISymbol> Members = members;
    }

    internal partial struct NotifyDataErrorInfoGenerator
    {
        internal const string NotifyDataErrorInfoAttributeFullyQualifiedMetadataName = "Minimal.Mvvm.NotifyDataErrorInfoAttribute";

        #region Pipeline

        internal static bool IsValidSyntaxNode(SyntaxNode attributeTarget, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            //Trace.WriteLine($"pipeline syntaxNode={attributeTarget}");
            return attributeTarget is ClassDeclarationSyntax;
        }

        public static bool IsValidType(Compilation compilation, ITypeSymbol typeSymbol)
        {
            var baseTypeSymbol = compilation.GetTypeByMetadataName("System.ComponentModel.INotifyDataErrorInfo");
            if (baseTypeSymbol == null || !typeSymbol.ImplementsInterface(baseTypeSymbol))
            {
                return false;
            }

            if (!NotifyPropertyGenerator.IsValidContainingType(compilation, typeSymbol))
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Methods

        public static void Generate(scoped Generator.GeneratorContext genCtx, scoped NotifyDataErrorInfoGeneratorContext ctx, ref bool isFirst)
        {
            foreach (var member in ctx.Members)
            {
                if (member is not ITypeSymbol typeSymbol)
                {
                    Trace.WriteLine($"{member} is not a ITypeSymbol");
                    continue;
                }
                GenerateForMember(genCtx, ctx, typeSymbol, ref isFirst);
            }
        }

        private static void GenerateForMember(scoped Generator.GeneratorContext genCtx, scoped NotifyDataErrorInfoGeneratorContext ctx, 
            ITypeSymbol typeSymbol, ref bool isFirst)
        {
            string nullable = genCtx.Compilation.Options.NullableContextOptions.HasFlag(NullableContextOptions.Annotations) ? "?" : string.Empty;

            var code = GetCodeSource(nullable, EventArgsCacheGenerator.GeneratedClassFullyQualifiedName, DataErrorsChangedEventArgsCacheGenerator.GeneratedClassFullyQualifiedName);
            var lines = Generator.GetSourceLines(code);

            if (!isFirst)
            {
                ctx.Writer.WriteLineNoTabs(string.Empty);
            }
            isFirst = false;

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
            genCtx.CachedPropertyNames.Add(nameof(INotifyDataErrorInfo.HasErrors));
            genCtx.NotifyDataErrorTypeNames.Add(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        #endregion

        internal static string GetCodeSource(int indentation, string nullable)
        {
            var code = GetCodeSource(nullable, EventArgsCacheGenerator.GeneratedClassFullyQualifiedName, DataErrorsChangedEventArgsCacheGenerator.GeneratedClassFullyQualifiedName);
            var lines = Generator.GetSourceLines(code);
            var sb = new StringBuilder(8192);
            foreach (var (indent, length, line) in lines)
            {
                if (length == 0 || Generator.IsNoTabsLine(line))
                {
                    sb.AppendLine(line);
                    continue;
                }
                var i = indent + indentation;
                if (i > 0)
                {
                    sb.Append(' ', i * 4);
                }
                sb.Append(line);
                sb.AppendLine();
            }
            return sb.ToString();
        }

    }
}
