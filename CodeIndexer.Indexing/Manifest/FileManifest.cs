namespace CodeIndexer.Indexing.Manifest;

/// <summary>
/// Tracks the content hash of every indexed file at the time it was last
/// parsed, so a later run can tell, per file, whether it's unchanged (skip),
/// changed (re-parse), or gone (drop its nodes) — the file-granularity
/// change detection an incremental update needs.
/// </summary>
public sealed record FileManifest
{
    /// <summary>File path -> content hash, as of the last successful parse of that file.</summary>
    public required IReadOnlyDictionary<string, string> FileHashes { get; init; }

    public static FileManifest Empty { get; } = new() { FileHashes = new Dictionary<string, string>() };
}
