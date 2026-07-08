namespace CodeIndexer.Indexing.GitHooks;

/// <summary>Outcome of installing (or trying to install) the git hooks for a session.</summary>
public sealed record GitHookInstallResult
{
    public required bool Success { get; init; }

    public string? HooksDirectory { get; init; }

    public IReadOnlyList<string> InstalledHookNames { get; init; } = Array.Empty<string>();

    public string? ErrorMessage { get; init; }

    public static GitHookInstallResult Ok(string hooksDirectory, IReadOnlyList<string> installed) =>
        new() { Success = true, HooksDirectory = hooksDirectory, InstalledHookNames = installed };

    public static GitHookInstallResult Failed(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}
