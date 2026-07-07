using CodeIndexer.Core.Nodes;
using CodeIndexer.Core.Parsing;
using CodeIndexer.Indexing;
using CodeIndexer.Indexing.Sessions;
using CodeIndexer.Parsing.CSharp;
using CodeIndexer.Search;
using CodeIndexer.Search.Structure;
using CodeIndexer.Storage;
using Xunit;

namespace CodeIndexer.Tests.Integration;

/// <summary>
/// Exercises the whole v1 loop for real: write source files to disk, run the
/// actual orchestrator (discovery + Roslyn parsing + binary storage), then
/// drive search, retrieval, and structure views against what got persisted.
/// No component is mocked — this is what the CLI does end to end.
/// </summary>
public class EndToEndTests : IDisposable
{
    private readonly string _root;
    private readonly IReadOnlyList<ICodeParser> _parsers = new ICodeParser[] { new CSharpParser() };

    public EndToEndTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "codeindex-e2e-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
    }

    private void WriteSource(string relativePath, string content)
    {
        var fullPath = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private async Task<(Session Session, IReadOnlyList<CodeNode> Nodes, IndexRunResult Result)> IndexAsync()
    {
        var sessionManager = new SessionManager(new SessionRegistry(Path.Combine(_root, "registry.json")));
        var session = sessionManager.EnsureSession(_root);

        var orchestrator = new IndexOrchestrator(_parsers);
        var result = await orchestrator.RunFullIndexAsync(session, CancellationToken.None);

        var readResult = new BinaryIndexStore().Read(session.IndexFilePath);
        Assert.True(readResult.Success, readResult.Detail);

        return (session, readResult.Nodes, result);
    }

    [Fact]
    public async Task FullPipeline_IndexSearchRetrieve_WorksEndToEnd()
    {
        WriteSource("src/UserService.cs", """
            namespace SampleApp.Services;

            /// <summary>Looks up users.</summary>
            public class UserService
            {
                public async Task<string> GetUserAsync(int id)
                {
                    return await Task.FromResult("user-" + id);
                }
            }
            """);

        var (_, nodes, result) = await IndexAsync();

        Assert.Equal(1, result.FilesDiscovered);
        Assert.Empty(result.SkippedFiles);

        var hits = new NodeSearchEngine().Search(nodes, new SearchQuery { NamePattern = "GetUser" });
        var hit = Assert.Single(hits);
        Assert.Equal("SampleApp.Services.UserService.GetUserAsync", hit.QualifiedName);

        var classNode = Assert.Single(nodes, n => n.Kind == NodeKind.Class);
        Assert.Equal("Looks up users.", classNode.Summary.DocComment);

        var code = new NodeRetriever().GetCode(nodes, hit.Id);
        Assert.True(code.Found);
        Assert.Contains("Task.FromResult", code.Body);
        Assert.Equal(nodes.First(n => n.Id == hit.Id).ContentHash, code.ContentHash);
    }

    [Fact]
    public async Task FullPipeline_RespectsExcludedDirectoriesAndGitignore()
    {
        WriteSource("src/App.cs", "namespace App; public class Real {}");
        WriteSource("bin/Debug/Generated.cs", "namespace App; public class ShouldBeSkipped {}");
        WriteSource("vendor/Third.cs", "namespace App; public class AlsoSkipped {}");
        WriteSource(".gitignore", "vendor/\n");

        var (_, nodes, result) = await IndexAsync();

        Assert.Equal(1, result.FilesDiscovered);
        Assert.Contains(nodes, n => n.Name == "Real");
        Assert.DoesNotContain(nodes, n => n.Name == "ShouldBeSkipped");
        Assert.DoesNotContain(nodes, n => n.Name == "AlsoSkipped");
    }

    [Fact]
    public async Task FullPipeline_SyntaxErrorFile_IsSkippedButOthersStillIndexed()
    {
        WriteSource("src/Good.cs", "namespace App; public class Good {}");
        WriteSource("src/Bad.cs", "public class {{{ totally broken");

        var (_, nodes, result) = await IndexAsync();

        Assert.Equal(2, result.FilesDiscovered);
        Assert.Single(result.SkippedFiles);
        Assert.Contains("Bad.cs", result.SkippedFiles[0]);
        Assert.Contains(nodes, n => n.Name == "Good");
    }

    [Fact]
    public async Task FullPipeline_ReIndex_OverwritesPreviousResultsAtomically()
    {
        WriteSource("src/App.cs", "namespace App; public class First {}");
        await IndexAsync();

        File.WriteAllText(Path.Combine(_root, "src/App.cs"), "namespace App; public class Second {}");
        var (_, nodes, _) = await IndexAsync();

        Assert.DoesNotContain(nodes, n => n.Name == "First");
        Assert.Contains(nodes, n => n.Name == "Second");
    }

    [Fact]
    public async Task FullPipeline_SessionResolvesFromNestedChildDirectory()
    {
        WriteSource("src/deep/nested/App.cs", "namespace App; public class Foo {}");
        var (session, _, _) = await IndexAsync();

        var sessionManager = new SessionManager(new SessionRegistry(Path.Combine(_root, "registry.json")));
        var childDir = Path.Combine(_root, "src", "deep", "nested");

        var resolution = sessionManager.TryResolve(childDir);

        Assert.True(resolution.Found);
        Assert.Equal(session.RootPath, resolution.Session!.RootPath);
    }

    [Fact]
    public async Task FullPipeline_StructureViews_ReflectIndexedNodes()
    {
        WriteSource("src/Services/UserService.cs", """
            namespace SampleApp.Services;
            public class UserService
            {
                public void GetUser() {}
            }
            """);
        WriteSource("src/Models/User.cs", """
            namespace SampleApp.Models;
            public class User
            {
                public string Name;
            }
            """);

        var (session, nodes, _) = await IndexAsync();

        var files = nodes.Select(n => n.Location.FilePath).Distinct().ToArray();

        var tree = DirectoryTreeBuilder.Build(session.RootPath, files);
        var servicesDir = Assert.Single(tree.Children, c => c.Name == "src");
        Assert.Contains(servicesDir.Children, c => c.Name == "Services");
        Assert.Contains(servicesDir.Children, c => c.Name == "Models");

        var fileOutlines = FileOutlineBuilder.Build(nodes);
        Assert.Equal(2, fileOutlines.Count);
        Assert.Contains(fileOutlines, o => o.Nodes.Any(n => n.Name == "UserService"));

        var scopeOutline = ScopeOutlineBuilder.Build(nodes);
        var sampleApp = Assert.Single(scopeOutline, n => n.Name == "SampleApp");
        Assert.Contains(sampleApp.Children, c => c.Name == "Services");
        Assert.Contains(sampleApp.Children, c => c.Name == "Models");

        var located = FileLocator.Locate(files, "UserService");
        Assert.Single(located);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
