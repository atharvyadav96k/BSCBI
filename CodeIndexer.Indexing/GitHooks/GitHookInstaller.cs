using System.Runtime.InteropServices;
using CodeIndexer.Indexing.Sessions;

namespace CodeIndexer.Indexing.GitHooks;

/// <summary>
/// Installs post-commit, post-merge, and post-checkout hooks that run an
/// incremental update in the background — never pre-commit (must not slow or
/// block a commit), and never just post-commit alone (pulls and branch
/// switches change files that never touch this machine's commit hook).
/// </summary>
public static class GitHookInstaller
{
    private const string Marker = "# Installed by CodeIndexer — do not edit by hand, re-run install-hooks instead.";

    private static readonly string[] HookNames = { "post-commit", "post-merge", "post-checkout" };

    public static GitHookInstallResult Install(Session session, string executablePath)
    {
        var hooksDirectory = GitRepositoryLocator.FindHooksDirectory(session.RootPath);
        if (hooksDirectory is null)
        {
            return GitHookInstallResult.Failed(
                $"No .git directory found above '{session.RootPath}'. Run 'git init' first, or install hooks from inside the git repository.");
        }

        Directory.CreateDirectory(hooksDirectory);

        var installed = new List<string>();
        var skipped = new List<string>();

        foreach (var hookName in HookNames)
        {
            var hookPath = Path.Combine(hooksDirectory, hookName);

            if (File.Exists(hookPath) && !File.ReadAllText(hookPath).Contains(Marker, StringComparison.Ordinal))
            {
                skipped.Add(hookName);
                continue;
            }

            File.WriteAllText(hookPath, BuildHookScript(session.RootPath, executablePath));
            MakeExecutable(hookPath);
            installed.Add(hookName);
        }

        if (skipped.Count > 0)
        {
            return GitHookInstallResult.Failed(
                $"Installed {string.Join(", ", installed)}, but skipped existing hook(s) not managed by CodeIndexer: {string.Join(", ", skipped)}. Remove or back them up, then re-run install-hooks.");
        }

        return GitHookInstallResult.Ok(hooksDirectory, installed);
    }

    private static string BuildHookScript(string repoRoot, string executablePath)
    {
        var logPath = Path.Combine(SessionPaths.MarkerDirectory(repoRoot), "hook.log").Replace('\\', '/');
        var normalizedRoot = repoRoot.Replace('\\', '/');
        var normalizedExe = executablePath.Replace('\\', '/');

        return $"""
            #!/bin/sh
            {Marker}
            # Runs in the background so this hook never slows down or blocks git.
            nohup "{normalizedExe}" update "{normalizedRoot}" > "{logPath}" 2>&1 &
            exit 0
            """.Replace("\r\n", "\n");
    }

    private static void MakeExecutable(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Git for Windows runs hooks via its bundled sh regardless of the
            // Windows executable bit, so there's nothing to set here.
            return;
        }

        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }
}
