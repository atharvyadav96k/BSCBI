using CodeIndexer.Core.Nodes;

namespace CodeIndexer.Indexing.Manifest;

/// <summary>Diffs currently-discovered files against a previous <see cref="FileManifest"/>.</summary>
public static class FileChangeDetector
{
    public static FileChangeSet Detect(IReadOnlyList<string> discoveredFiles, FileManifest oldManifest)
    {
        var added = new List<string>();
        var changed = new List<string>();
        var unchanged = new List<string>();
        var currentHashes = new Dictionary<string, string>();

        foreach (var file in discoveredFiles)
        {
            var currentHash = ContentHasher.Hash(File.ReadAllText(file));
            currentHashes[file] = currentHash;

            if (!oldManifest.FileHashes.TryGetValue(file, out var oldHash))
            {
                added.Add(file);
            }
            else if (!string.Equals(oldHash, currentHash, StringComparison.Ordinal))
            {
                changed.Add(file);
            }
            else
            {
                unchanged.Add(file);
            }
        }

        var discoveredSet = new HashSet<string>(discoveredFiles, StringComparer.OrdinalIgnoreCase);
        var removed = oldManifest.FileHashes.Keys.Where(f => !discoveredSet.Contains(f)).ToArray();

        return new FileChangeSet { Added = added, Changed = changed, Removed = removed, Unchanged = unchanged, CurrentHashes = currentHashes };
    }
}
