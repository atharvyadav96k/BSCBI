namespace CodeIndexer.Indexing.GitHooks;

/// <summary>Finds the real ".git" hooks directory for a working directory, including worktree checkouts.</summary>
public static class GitRepositoryLocator
{
    /// <summary>Walks up from <paramref name="startDirectory"/> looking for a ".git" entry; returns null if none found.</summary>
    public static string? FindHooksDirectory(string startDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));

        while (current is not null)
        {
            var gitPath = Path.Combine(current.FullName, ".git");

            if (Directory.Exists(gitPath))
            {
                return Path.Combine(gitPath, "hooks");
            }

            if (File.Exists(gitPath))
            {
                // Worktree/submodule checkout: ".git" is a file containing "gitdir: <real path>".
                var line = File.ReadAllText(gitPath).Trim();
                const string prefix = "gitdir:";
                if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    var realGitDir = line[prefix.Length..].Trim();
                    if (!Path.IsPathRooted(realGitDir))
                    {
                        realGitDir = Path.GetFullPath(Path.Combine(current.FullName, realGitDir));
                    }

                    return Path.Combine(realGitDir, "hooks");
                }
            }

            current = current.Parent;
        }

        return null;
    }
}
