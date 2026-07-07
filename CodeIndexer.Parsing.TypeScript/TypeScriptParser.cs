using CodeIndexer.Core.Parsing;
using CodeIndexer.Parsing.TypeScript.Internal;
using Zu.TypeScript;

namespace CodeIndexer.Parsing.TypeScript;

/// <summary>
/// The TypeScript implementation of <see cref="ICodeParser"/>, backed by
/// TypeScriptAST (a .NET port of the TypeScript compiler's own parser). This
/// is the only place in the repository allowed to reference TypeScriptAST
/// types or encode TS-specific assumptions.
/// </summary>
/// <remarks>
/// Deliberately does not claim ".tsx" — this library parses JSX's angle-bracket
/// syntax as TypeScript's old-style type-assertion casts instead of JSX
/// elements, producing a silently wrong (not merely absent) tree.
/// </remarks>
public sealed class TypeScriptParser : ICodeParser
{
    public IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".ts", ".mts", ".cts" };

    public string ScopeSeparator => ".";

    public Task<ParseResult> ParseFileAsync(string filePath, string sourceText, CancellationToken cancellationToken)
    {
        try
        {
            var ast = new TypeScriptAST(sourceText, filePath);
            var walker = new TsNodeWalker(filePath, sourceText);
            walker.Walk(ast.RootNode);

            return Task.FromResult(ParseResult.Ok(walker.Nodes));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ParseResult.Failed($"Failed to parse {filePath}: {ex.Message}"));
        }
    }
}
