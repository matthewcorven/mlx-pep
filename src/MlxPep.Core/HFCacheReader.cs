namespace MlxPep.Core;

public interface IHFCacheReader
{
    Task<IEnumerable<Model>> ListModelsAsync();
    Task<Model?> GetModelAsync(string repoId);
}

public class HFCacheReader : IHFCacheReader
{
    private readonly string _cacheDir;

    public HFCacheReader(string? cacheDir = null)
    {
        // Honor HF_HOME, HF_HUB_CACHE, or default to ~/.cache/huggingface/hub
        _cacheDir = cacheDir
            ?? Environment.GetEnvironmentVariable("HF_HUB_CACHE")
            ?? Path.Combine(
                Environment.GetEnvironmentVariable("HF_HOME") 
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "huggingface"),
                "hub");
    }

    public async Task<IEnumerable<Model>> ListModelsAsync()
    {
        var models = new List<Model>();

        if (!Directory.Exists(_cacheDir))
            return models;

        // Parse models--<org>--<name> directories
        foreach (var modelDir in Directory.EnumerateDirectories(_cacheDir, "models--*"))
        {
            var repoId = ParseRepoIdFromDir(modelDir);
            if (string.IsNullOrEmpty(repoId))
                continue;

            // Get revisions from refs/
            var refsDir = Path.Combine(modelDir, "refs");
            if (Directory.Exists(refsDir))
            {
                foreach (var refFile in Directory.EnumerateFiles(refsDir))
                {
                    var revision = Path.GetFileName(refFile);
                    var size = CalculateModelSize(modelDir);
                    var lastModified = GetLastModified(modelDir);

                    models.Add(new Model(repoId, revision, size, lastModified));
                }
            }
        }

        return await Task.FromResult(models);
    }

    public async Task<Model?> GetModelAsync(string repoId)
    {
        var models = await ListModelsAsync();
        return models.FirstOrDefault(m => m.RepoId.Equals(repoId, StringComparison.OrdinalIgnoreCase));
    }

    private string? ParseRepoIdFromDir(string modelDir)
    {
        var dirName = Path.GetFileName(modelDir);
        if (!dirName.StartsWith("models--"))
            return null;

        // Convert models--org--name to org/name
        var parts = dirName.Substring(8).Split("--");
        if (parts.Length == 2)
            return $"{parts[0]}/{parts[1]}";

        return null;
    }

    private long CalculateModelSize(string modelDir)
    {
        long totalSize = 0;

        try
        {
            var snapshotsDir = Path.Combine(modelDir, "snapshots");
            if (Directory.Exists(snapshotsDir))
            {
                foreach (var file in Directory.EnumerateFiles(snapshotsDir, "*", SearchOption.AllDirectories))
                {
                    var info = new FileInfo(file);
                    totalSize += info.Length;
                }
            }
        }
        catch
        {
            // Ignore errors reading file sizes
        }

        return totalSize;
    }

    private DateTime GetLastModified(string modelDir)
    {
        try
        {
            var snapshotsDir = Path.Combine(modelDir, "snapshots");
            if (Directory.Exists(snapshotsDir))
            {
                return Directory.EnumerateFiles(snapshotsDir, "*", SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f).LastWriteTimeUtc)
                    .OrderByDescending(d => d)
                    .FirstOrDefault();
            }
        }
        catch
        {
            // Ignore errors
        }

        return DateTime.UtcNow;
    }
}
