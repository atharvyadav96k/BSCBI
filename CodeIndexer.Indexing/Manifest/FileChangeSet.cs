namespace CodeIndexer.Indexing.Manifest;

/// <summary>
/// The result of diffing currently-discovered files against a <see cref="FileManifest"/>:
/// what changed since the manifest was last written.
/// </summary>
public sealed record FileChangeSet
{
    /// <summary>Files not in the manifest at all — need parsing for the first time.</summary>
    public required IReadOnlyList<string> Added { get; init; }

    /// <summary>Files in the manifest whose content hash no longer matches — need re-parsing.</summary>
    public required IReadOnlyList<string> Changed { get; init; }

    /// <summary>Files in the manifest that no longer exist/weren't discovered — their nodes must be dropped.</summary>
    public required IReadOnlyList<string> Removed { get; init; }

    /// <summary>Files whose hash still matches the manifest — safe to carry forward unparsed.</summary>
    public required IReadOnlyList<string> Unchanged { get; init; }

    /// <summary>Every discovered file's freshly-computed hash, so callers never need to re-hash to write the new manifest.</summary>
    public required IReadOnlyDictionary<string, string> CurrentHashes { get; init; }

    public bool IsClean => Added.Count == 0 && Changed.Count == 0 && Removed.Count == 0;
}
