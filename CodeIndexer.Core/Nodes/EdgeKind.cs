namespace CodeIndexer.Core.Nodes;

/// <summary>
/// The fixed taxonomy of relationships between nodes (Phase 7). Declared now so
/// the node model's shape is stable from Phase 0 onward, even though no parser
/// populates edges until relationship extraction ships.
/// </summary>
public enum EdgeKind
{
    Contains,
    Calls,
    Inherits,
    Implements,
    Imports,

    /// <summary>
    /// A method parameter, field, or property whose declared type is another
    /// indexed Class/Interface — covers usages that are neither a call nor an
    /// inheritance relationship, most commonly constructor-injected
    /// dependencies (e.g. "constructor(private authService: AuthService)").
    /// </summary>
    References,
}
