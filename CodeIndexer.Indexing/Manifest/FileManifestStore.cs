using System.Text.Json;

namespace CodeIndexer.Indexing.Manifest;

/// <summary>Reads and writes a session's file manifest (small JSON, alongside session.json).</summary>
public static class FileManifestStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static void Write(string manifestFilePath, FileManifest manifest)
    {
        var json = JsonSerializer.Serialize(manifest.FileHashes, Options);
        File.WriteAllText(manifestFilePath, json);
    }

    /// <summary>Returns <see cref="FileManifest.Empty"/> if no manifest exists yet (first run, or pre-Phase-9 index).</summary>
    public static FileManifest Read(string manifestFilePath)
    {
        if (!File.Exists(manifestFilePath))
        {
            return FileManifest.Empty;
        }

        var json = File.ReadAllText(manifestFilePath);
        var hashes = JsonSerializer.Deserialize<Dictionary<string, string>>(json, Options);
        return new FileManifest { FileHashes = hashes ?? new Dictionary<string, string>() };
    }
}
