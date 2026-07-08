using CodeIndexer.Indexing.GitHooks;
using CodeIndexer.Indexing.Sessions;
using Xunit;

namespace CodeIndexer.Tests.Indexing;

public class GitHookInstallerTests : IDisposable
{
    private readonly string _repoRoot;

    public GitHookInstallerTests()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), "codeindex-githooks-" + Guid.NewGuid());
        Directory.CreateDirectory(_repoRoot);
    }

    private Session MakeSession() => new() { RootPath = _repoRoot };

    [Fact]
    public void Install_NoGitDirectory_FailsWithClearMessage()
    {
        var result = GitHookInstaller.Install(MakeSession(), "/usr/bin/codeindexer");

        Assert.False(result.Success);
        Assert.Contains(".git", result.ErrorMessage);
    }

    [Fact]
    public void Install_WritesAllThreeHooks_NeverPreCommit()
    {
        Directory.CreateDirectory(Path.Combine(_repoRoot, ".git"));

        var result = GitHookInstaller.Install(MakeSession(), "/usr/bin/codeindexer");

        Assert.True(result.Success);
        Assert.Equal(new[] { "post-commit", "post-merge", "post-checkout" }, result.InstalledHookNames);
        Assert.False(File.Exists(Path.Combine(_repoRoot, ".git", "hooks", "pre-commit")));
    }

    [Fact]
    public void Install_HookScript_BackgroundsAndAlwaysExitsZero()
    {
        Directory.CreateDirectory(Path.Combine(_repoRoot, ".git"));

        GitHookInstaller.Install(MakeSession(), "/usr/bin/codeindexer");

        var script = File.ReadAllText(Path.Combine(_repoRoot, ".git", "hooks", "post-commit"));
        Assert.Contains("&", script);
        Assert.Contains("exit 0", script);
        Assert.StartsWith("#!/bin/sh", script);
    }

    [Fact]
    public void Install_ExistingUnmanagedHook_IsNotOverwritten()
    {
        var hooksDir = Path.Combine(_repoRoot, ".git", "hooks");
        Directory.CreateDirectory(hooksDir);
        File.WriteAllText(Path.Combine(hooksDir, "post-commit"), "#!/bin/sh\necho 'my custom hook'\n");

        var result = GitHookInstaller.Install(MakeSession(), "/usr/bin/codeindexer");

        Assert.False(result.Success);
        Assert.Contains("post-commit", result.ErrorMessage);
        Assert.Contains("my custom hook", File.ReadAllText(Path.Combine(hooksDir, "post-commit")));
    }

    [Fact]
    public void Install_CalledTwice_ReinstallsOwnHookWithoutComplaint()
    {
        Directory.CreateDirectory(Path.Combine(_repoRoot, ".git"));

        GitHookInstaller.Install(MakeSession(), "/usr/bin/codeindexer");
        var second = GitHookInstaller.Install(MakeSession(), "/usr/bin/codeindexer");

        Assert.True(second.Success);
    }

    [Fact]
    public void FindHooksDirectory_WorktreeStyleGitFile_ResolvesRealGitDir()
    {
        var realGitDir = Path.Combine(Path.GetTempPath(), "codeindex-realgit-" + Guid.NewGuid());
        Directory.CreateDirectory(realGitDir);
        File.WriteAllText(Path.Combine(_repoRoot, ".git"), $"gitdir: {realGitDir}\n");

        try
        {
            var hooksDir = GitRepositoryLocator.FindHooksDirectory(_repoRoot);

            Assert.Equal(Path.Combine(realGitDir, "hooks"), hooksDir);
        }
        finally
        {
            Directory.Delete(realGitDir, recursive: true);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_repoRoot))
        {
            Directory.Delete(_repoRoot, recursive: true);
        }
    }
}
