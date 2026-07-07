using CodeIndexer.Core.Nodes;
using CodeIndexer.Search.Structure;
using Xunit;

namespace CodeIndexer.Tests.Search;

public class StructureTests : IDisposable
{
    private readonly string _tempRoot;

    public StructureTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "codeindex-structure-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempRoot);
    }

    private static CodeNode MakeNode(string name, string qualifiedName, NodeKind kind, string filePath, int startLine = 1)
    {
        return new CodeNode
        {
            Id = qualifiedName,
            Name = name,
            ScopeChain = qualifiedName.Split('.'),
            ScopeSeparator = ".",
            QualifiedName = qualifiedName,
            Kind = kind,
            Location = new NodeLocation { FilePath = filePath, StartLine = startLine, EndLine = startLine + 3 },
            Summary = new NodeSummary { Name = name, Signature = name, Parameters = Array.Empty<ParameterInfo>(), LineCount = 4 },
            Body = name,
            Metadata = new NodeMetadata(),
            ContentHash = ContentHasher.Hash(name),
        };
    }

    [Fact]
    public void DirectoryTreeBuilder_BuildsNestedFolders()
    {
        var files = new[]
        {
            Path.Combine(_tempRoot, "src", "App.cs"),
            Path.Combine(_tempRoot, "src", "nested", "Deep.cs"),
            Path.Combine(_tempRoot, "readme.md"),
        };

        var tree = DirectoryTreeBuilder.Build(_tempRoot, files);

        var src = Assert.Single(tree.Children, c => c.Name == "src");
        Assert.True(src.IsDirectory);
        Assert.Contains(src.Children, c => c.Name == "App.cs" && !c.IsDirectory);
        var nested = Assert.Single(src.Children, c => c.Name == "nested");
        Assert.Contains(nested.Children, c => c.Name == "Deep.cs");
        Assert.Contains(tree.Children, c => c.Name == "readme.md");
    }

    [Fact]
    public void FileOutlineBuilder_GroupsNodesByFile()
    {
        var nodes = new[]
        {
            MakeNode("Foo", "App.Foo", NodeKind.Class, "App.cs", 1),
            MakeNode("Bar", "App.Foo.Bar", NodeKind.Method, "App.cs", 5),
            MakeNode("Baz", "App.Baz", NodeKind.Class, "Other.cs", 1),
        };

        var outlines = FileOutlineBuilder.Build(nodes);

        Assert.Equal(2, outlines.Count);
        var appFile = Assert.Single(outlines, o => o.FilePath == "App.cs");
        Assert.Equal(2, appFile.Nodes.Count);
        Assert.Equal("Foo", appFile.Nodes[0].Name);
        Assert.Equal("Bar", appFile.Nodes[1].Name);
    }

    [Fact]
    public void ScopeOutlineBuilder_ImportSharingNamespacePath_DoesNotOverwriteNamespaceKind()
    {
        var namespaceNode = MakeNode("App.Models", "App.Models", NodeKind.Namespace, "Models/User.cs");
        // An import's own scope chain is a single unsplit segment (e.g. "App.Models"),
        // exactly like the real CSharpNodeWalker emits it, so it collides with the
        // namespace's path once both get split on '.' during outline building.
        var importNode = namespaceNode with
        {
            Id = "import-id",
            Name = "App.Models",
            ScopeChain = new[] { "App.Models" },
            Kind = NodeKind.Import,
            Location = namespaceNode.Location with { FilePath = "Controllers/OrdersController.cs" },
        };

        var outline = ScopeOutlineBuilder.Build(new[] { importNode, namespaceNode });

        var app = Assert.Single(outline, n => n.Name == "App");
        var models = Assert.Single(app.Children, n => n.Name == "Models");
        Assert.Equal(NodeKind.Namespace, models.Kind);
    }

    [Fact]
    public void ScopeOutlineBuilder_NestsByDottedNamespaceSegments()
    {
        var nodes = new[]
        {
            MakeNode("MyApp.Services", "MyApp.Services", NodeKind.Namespace, "App.cs"),
            MakeNode("UserService", "MyApp.Services.UserService", NodeKind.Class, "App.cs"),
            MakeNode("GetUser", "MyApp.Services.UserService.GetUser", NodeKind.Method, "App.cs"),
        };

        var outline = ScopeOutlineBuilder.Build(nodes);

        var myApp = Assert.Single(outline, n => n.Name == "MyApp");
        Assert.Null(myApp.Kind);
        var services = Assert.Single(myApp.Children, n => n.Name == "Services");
        Assert.Equal(NodeKind.Namespace, services.Kind);
        var userService = Assert.Single(services.Children, n => n.Name == "UserService");
        Assert.Equal(NodeKind.Class, userService.Kind);
        var getUser = Assert.Single(userService.Children, n => n.Name == "GetUser");
        Assert.Equal(NodeKind.Method, getUser.Kind);
    }

    [Fact]
    public void FileLocator_FindsByExactFilenameFirst()
    {
        var files = new[] { "src/App.cs", "src/nested/App.cs", "src/AppSettings.cs" };

        var results = FileLocator.Locate(files, "App.cs");

        Assert.Equal(2, results.Count);
        Assert.Contains(results[0], new[] { "src/App.cs", "src/nested/App.cs" });
    }

    [Fact]
    public void FileLocator_MatchesPathFragment()
    {
        var files = new[] { "src/Services/UserService.cs", "src/Models/User.cs" };

        var results = FileLocator.Locate(files, "Services");

        Assert.Single(results);
        Assert.Equal("src/Services/UserService.cs", results[0]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
