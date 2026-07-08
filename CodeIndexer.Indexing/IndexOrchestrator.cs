using CodeIndexer.Core.Nodes;
using CodeIndexer.Core.Parsing;
using CodeIndexer.Indexing.Discovery;
using CodeIndexer.Indexing.Manifest;
using CodeIndexer.Indexing.Relationships;
using CodeIndexer.Indexing.Sessions;
using CodeIndexer.Storage;

namespace CodeIndexer.Indexing;

/// <summary>
/// Drives indexing of a session: discover files, dispatch each to the parser
/// that owns its extension, and persist the resulting nodes. Talks to parsers
/// only through <see cref="ICodeParser"/> — it has no idea which languages are
/// registered. Supports both a full re-index (the source of truth) and a
/// file-granularity incremental update on top of it.
/// </summary>
public sealed class IndexOrchestrator
{
    private readonly IReadOnlyList<ICodeParser> _parsers;
    private readonly FileDiscoverer _fileDiscoverer;
    private readonly BinaryIndexStore _indexStore;

    public IndexOrchestrator(IReadOnlyList<ICodeParser> parsers)
    {
        _parsers = parsers;
        _fileDiscoverer = new FileDiscoverer();
        _indexStore = new BinaryIndexStore();
    }

    public async Task<IndexRunResult> RunFullIndexAsync(Session session, CancellationToken cancellationToken)
    {
        var extensionToParser = BuildExtensionMap();
        var files = DiscoverFiles(session, extensionToParser);

        var (nodes, skipped) = await ParseFilesAsync(files, extensionToParser, cancellationToken);

        var resolvedNodes = RelationshipResolver.Resolve(nodes);
        _indexStore.Write(session.IndexFilePath, resolvedNodes);
        WriteManifest(session, files);

        return new IndexRunResult
        {
            FilesDiscovered = files.Count,
            NodesIndexed = resolvedNodes.Count,
            SkippedFiles = skipped,
        };
    }

    /// <summary>
    /// Re-parses only files whose content changed since the last index/update,
    /// carries forward nodes from unchanged files, and drops nodes for files
    /// that no longer exist. Falls back to a full index if there's no usable
    /// prior manifest/index to diff against (e.g. the first run, or an index
    /// written before Phase 9).
    /// </summary>
    public async Task<IncrementalIndexResult> RunIncrementalIndexAsync(Session session, CancellationToken cancellationToken)
    {
        var extensionToParser = BuildExtensionMap();
        var files = DiscoverFiles(session, extensionToParser);

        var oldManifest = FileManifestStore.Read(session.ManifestFilePath);
        var previousRead = _indexStore.Read(session.IndexFilePath);

        if (oldManifest.FileHashes.Count == 0 || !previousRead.Success)
        {
            var (allNodes, allSkipped) = await ParseFilesAsync(files, extensionToParser, cancellationToken);
            var resolvedAll = RelationshipResolver.Resolve(allNodes);
            _indexStore.Write(session.IndexFilePath, resolvedAll);
            WriteManifest(session, files);

            return new IncrementalIndexResult
            {
                FilesAdded = files.Count,
                FilesChanged = 0,
                FilesRemoved = 0,
                FilesUnchanged = 0,
                NodesIndexed = resolvedAll.Count,
                SkippedFiles = allSkipped,
                FellBackToFullIndex = true,
            };
        }

        var changeSet = FileChangeDetector.Detect(files, oldManifest);

        var unchangedSet = new HashSet<string>(changeSet.Unchanged, StringComparer.OrdinalIgnoreCase);
        var carriedForwardNodes = previousRead.Nodes.Where(n => unchangedSet.Contains(n.Location.FilePath)).ToList();

        var filesToParse = changeSet.Added.Concat(changeSet.Changed).ToArray();
        var (newNodes, skipped) = await ParseFilesAsync(filesToParse, extensionToParser, cancellationToken);

        var mergedNodes = carriedForwardNodes.Concat(newNodes).ToArray();
        var resolvedNodes = RelationshipResolver.Resolve(mergedNodes);
        _indexStore.Write(session.IndexFilePath, resolvedNodes);

        var newManifest = new FileManifest { FileHashes = changeSet.CurrentHashes };
        FileManifestStore.Write(session.ManifestFilePath, newManifest);

        return new IncrementalIndexResult
        {
            FilesAdded = changeSet.Added.Count,
            FilesChanged = changeSet.Changed.Count,
            FilesRemoved = changeSet.Removed.Count,
            FilesUnchanged = changeSet.Unchanged.Count,
            NodesIndexed = resolvedNodes.Count,
            SkippedFiles = skipped,
            FellBackToFullIndex = false,
        };
    }

    /// <summary>Reports drift (added/changed/removed files) against the stored manifest without writing anything.</summary>
    public FileChangeSet DetectDrift(Session session)
    {
        var extensionToParser = BuildExtensionMap();
        var files = DiscoverFiles(session, extensionToParser);
        var manifest = FileManifestStore.Read(session.ManifestFilePath);
        return FileChangeDetector.Detect(files, manifest);
    }

    private Dictionary<string, ICodeParser> BuildExtensionMap()
    {
        var extensionToParser = new Dictionary<string, ICodeParser>(StringComparer.OrdinalIgnoreCase);
        foreach (var parser in _parsers)
        {
            foreach (var extension in parser.SupportedExtensions)
            {
                extensionToParser[extension] = parser;
            }
        }

        return extensionToParser;
    }

    private IReadOnlyList<string> DiscoverFiles(Session session, Dictionary<string, ICodeParser> extensionToParser)
    {
        var discoveryOptions = new FileDiscoveryOptions { IncludeExtensions = extensionToParser.Keys.ToArray() };
        return _fileDiscoverer.Discover(session.RootPath, discoveryOptions);
    }

    private void WriteManifest(Session session, IReadOnlyList<string> files)
    {
        var hashes = files.ToDictionary(f => f, f => ContentHasher.Hash(File.ReadAllText(f)));
        FileManifestStore.Write(session.ManifestFilePath, new FileManifest { FileHashes = hashes });
    }

    private async Task<(List<CodeNode> Nodes, List<string> Skipped)> ParseFilesAsync(
        IReadOnlyList<string> files,
        Dictionary<string, ICodeParser> extensionToParser,
        CancellationToken cancellationToken)
    {
        var nodes = new List<CodeNode>();
        var skipped = new List<string>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parser = extensionToParser[Path.GetExtension(file)];
            var sourceText = await File.ReadAllTextAsync(file, cancellationToken);
            var result = await parser.ParseFileAsync(file, sourceText, cancellationToken);

            if (result.Success)
            {
                nodes.AddRange(result.Nodes);
            }
            else
            {
                skipped.Add($"{file}: {result.ErrorMessage}");
            }
        }

        return (nodes, skipped);
    }
}
