using CodeIndexer.Indexing.Manifest;
using Xunit;

namespace CodeIndexer.Tests.Indexing;

public class FileChangeDetectorTests : IDisposable
{
    private readonly string _tempDir;

    public FileChangeDetectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "codeindex-changedetect-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Detect_FileNotInManifest_IsAdded()
    {
        var file = WriteFile("New.cs", "class New {}");

        var changeSet = FileChangeDetector.Detect(new[] { file }, FileManifest.Empty);

        Assert.Contains(file, changeSet.Added);
        Assert.True(changeSet.CurrentHashes.ContainsKey(file));
    }

    [Fact]
    public void Detect_UnchangedFileContent_IsUnchanged()
    {
        var file = WriteFile("Same.cs", "class Same {}");
        var hash = CodeIndexer.Core.Nodes.ContentHasher.Hash(File.ReadAllText(file));
        var manifest = new FileManifest { FileHashes = new Dictionary<string, string> { [file] = hash } };

        var changeSet = FileChangeDetector.Detect(new[] { file }, manifest);

        Assert.Contains(file, changeSet.Unchanged);
        Assert.Empty(changeSet.Added);
        Assert.Empty(changeSet.Changed);
    }

    [Fact]
    public void Detect_ChangedFileContent_IsChanged()
    {
        var file = WriteFile("Changed.cs", "class Changed {}");
        var manifest = new FileManifest { FileHashes = new Dictionary<string, string> { [file] = "stale-hash" } };

        var changeSet = FileChangeDetector.Detect(new[] { file }, manifest);

        Assert.Contains(file, changeSet.Changed);
    }

    [Fact]
    public void Detect_FileInManifestButNotDiscovered_IsRemoved()
    {
        var deletedPath = Path.Combine(_tempDir, "Deleted.cs");
        var manifest = new FileManifest { FileHashes = new Dictionary<string, string> { [deletedPath] = "some-hash" } };

        var changeSet = FileChangeDetector.Detect(Array.Empty<string>(), manifest);

        Assert.Contains(deletedPath, changeSet.Removed);
    }

    [Fact]
    public void Detect_NoChanges_IsClean()
    {
        var file = WriteFile("Stable.cs", "class Stable {}");
        var hash = CodeIndexer.Core.Nodes.ContentHasher.Hash(File.ReadAllText(file));
        var manifest = new FileManifest { FileHashes = new Dictionary<string, string> { [file] = hash } };

        var changeSet = FileChangeDetector.Detect(new[] { file }, manifest);

        Assert.True(changeSet.IsClean);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
