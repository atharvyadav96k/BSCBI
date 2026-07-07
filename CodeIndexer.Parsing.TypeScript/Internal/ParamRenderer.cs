using CodeIndexer.Core.Nodes;
using Zu.TypeScript.TsTypes;

namespace CodeIndexer.Parsing.TypeScript.Internal;

/// <summary>Renders a TS parameter declaration into a browsable form, preserving its real type annotation.</summary>
internal static class ParamRenderer
{
    public static IReadOnlyList<ParameterInfo> RenderAll(IEnumerable<Node> parameters, string sourceText) =>
        parameters.OfType<ParameterDeclaration>().Select(p => Render(p, sourceText)).ToArray();

    private static ParameterInfo Render(ParameterDeclaration param, string sourceText)
    {
        var restPrefix = param.DotDotDotToken is not null ? "..." : string.Empty;
        var optionalMark = param.QuestionToken is not null ? "?" : string.Empty;
        var defaultText = param.Initializer is not null ? $" = {param.Initializer.GetText(sourceText)}" : string.Empty;
        var name = $"{restPrefix}{param.IdentifierStr}{optionalMark}{defaultText}";
        var type = param.Type?.GetText(sourceText) ?? "any";

        return new ParameterInfo { Name = name, Type = type };
    }
}
