namespace CodeIndexer.Parsing.TypeScript.Internal;

/// <summary>
/// Finds the doc comment immediately preceding a declaration by scanning
/// backward through the source text. Used instead of TypeScriptAST's own
/// JsDoc property, which only populates for single-line <c>/** ... */</c>
/// comments and silently returns empty text for multi-line ones.
/// </summary>
internal static class TsDocCommentExtractor
{
    public static string? FindFor(string sourceText, int declarationStart)
    {
        var i = declarationStart - 1;
        while (i >= 0 && char.IsWhiteSpace(sourceText[i]))
        {
            i--;
        }

        if (i < 1)
        {
            return null;
        }

        if (sourceText[i] == '/' && sourceText[i - 1] == '*')
        {
            var end = i + 1;
            var start = sourceText.LastIndexOf("/*", i - 1, StringComparison.Ordinal);
            return start < 0 ? null : CleanBlockComment(sourceText[start..end]);
        }

        var lineStart = sourceText.LastIndexOf('\n', i) + 1;
        var line = sourceText[lineStart..(i + 1)].TrimStart();
        if (line.StartsWith("//"))
        {
            var text = line[2..].Trim();
            return text.Length == 0 ? null : text;
        }

        return null;
    }

    private static string? CleanBlockComment(string raw)
    {
        var text = raw.Trim('/', '*');
        var lines = text.Split('\n')
            .Select(line => line.Trim().TrimStart('*').Trim())
            .Where(line => line.Length > 0);

        var cleaned = string.Join(" ", lines).Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }
}
