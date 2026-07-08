namespace CodeIndexer.Indexing;

/// <summary>Summary of one incremental index run, for reporting to the caller/AI.</summary>
public sealed record IncrementalIndexResult
{
    public required int FilesAdded { get; init; }

    public required int FilesChanged { get; init; }

    public required int FilesRemoved { get; init; }

    public required int FilesUnchanged { get; init; }

    public required int NodesIndexed { get; init; }

    /// <summary>Files that failed to parse, with the reason — never a crash, just a skip + log entry.</summary>
    public required IReadOnlyList<string> SkippedFiles { get; init; }

    /// <summary>True if the manifest/index was missing or unreadable, forcing a full re-parse instead of a true incremental update.</summary>
    public required bool FellBackToFullIndex { get; init; }
}
