namespace MlxPep.Core;

using System.Diagnostics;

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
        // Honor HF_HUB_CACHE first, then HF_HOME + /hub, then default to ~/.cache/huggingface/hub
        if (cacheDir != null)
        {
            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] Constructor: explicit cacheDir={cacheDir}");
            _cacheDir = cacheDir;
        }
        else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HF_HUB_CACHE")))
        {
            _cacheDir = Environment.GetEnvironmentVariable("HF_HUB_CACHE")!;
            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] Constructor: HF_HUB_CACHE is set, using {_cacheDir}");
        }
        else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HF_HOME")))
        {
            _cacheDir = Path.Combine(Environment.GetEnvironmentVariable("HF_HOME")!, "hub");
            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] Constructor: HF_HOME is set, using {_cacheDir}");
        }
        else
        {
            _cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cache",
                "huggingface",
                "hub");
            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] Constructor: using default cache directory {_cacheDir}");
        }
    }

    public async Task<IEnumerable<Model>> ListModelsAsync()
    {
        var models = new List<Model>();

        System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ListModelsAsync: checking cache directory {_cacheDir}");

        if (!Directory.Exists(_cacheDir))
        {
            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ListModelsAsync: cache directory does not exist, returning empty list");
            return models;
        }

        System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ListModelsAsync: cache directory exists, enumerating models--* directories");

        // Parse models--<org>--<name> directories
        try
        {
            var modelDirs = Directory.EnumerateDirectories(_cacheDir, "models--*").ToList();
            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ListModelsAsync: found {modelDirs.Count} model directories");

            foreach (var modelDir in modelDirs)
            {
                var dirName = Path.GetFileName(modelDir);
                System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ListModelsAsync: processing directory {dirName}");

                var repoId = ParseRepoIdFromDir(modelDir);
                if (string.IsNullOrEmpty(repoId))
                {
                    System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ListModelsAsync: failed to parse repo ID from {dirName}, skipping");
                    continue;
                }

                System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ListModelsAsync: parsed repoId={repoId}");

                // Get revisions from snapshots/ directory
                var snapshotsDir = Path.Combine(modelDir, "snapshots");
                if (!Directory.Exists(snapshotsDir))
                {
                    System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ListModelsAsync: snapshots directory does not exist for {repoId}, skipping");
                    continue;
                }

                System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ListModelsAsync: snapshots directory exists, enumerating revisions");

                // Each subdirectory in snapshots/ is a revision (commit hash)
                var revisionDirs = Directory.EnumerateDirectories(snapshotsDir).ToList();
                System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ListModelsAsync: found {revisionDirs.Count} revisions for {repoId}");

                foreach (var revisionDir in revisionDirs)
                {
                    // CRITICAL FIX #2: Validate path is within cache (prevent directory escape via symlinks)
                    if (!IsPathWithinCache(revisionDir))
                    {
                        System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ListModelsAsync: revision directory {revisionDir} is outside cache, skipping");
                        continue;
                    }

                    var revision = Path.GetFileName(revisionDir);
                    System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ListModelsAsync: processing revision {revision}");

                    try
                    {
                        var size = CalculateModelSize(revisionDir);
                        var lastModified = GetLastModified(revisionDir);

                        var model = new Model(repoId, revision, size, lastModified);
                        models.Add(model);
                        System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ListModelsAsync: added model {repoId}@{revision} (size={size} bytes, lastModified={lastModified:o})");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ListModelsAsync: permission denied for {revision}: {ex.Message}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ListModelsAsync: error processing revision {revision}: {ex.Message}");
                        continue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ListModelsAsync: error enumerating model directories: {ex.Message}");
        }

        System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ListModelsAsync: returning {models.Count} total models");
        return await Task.FromResult(models);
    }

    public async Task<Model?> GetModelAsync(string repoId)
    {
        System.Diagnostics.Debug.WriteLine($"[HFCacheReader] GetModelAsync: searching for repoId={repoId}");

        if (string.IsNullOrEmpty(repoId))
        {
            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] GetModelAsync: repoId is null or empty, returning null");
            return null;
        }

        var models = await ListModelsAsync();
        var result = models.FirstOrDefault(m => m.RepoId.Equals(repoId, StringComparison.OrdinalIgnoreCase));

        if (result != null)
        {
            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] GetModelAsync: found model {repoId}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] GetModelAsync: model {repoId} not found");
        }

        return result;
    }

    private string? ParseRepoIdFromDir(string modelDir)
    {
        var dirName = Path.GetFileName(modelDir) ?? string.Empty;
        System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ParseRepoIdFromDir: dirName={dirName}");

        if (!dirName.StartsWith("models--"))
        {
            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ParseRepoIdFromDir: directory name does not start with 'models--', returning null");
            return null;
        }

        // Convert models--org--name to org/name
        var parts = dirName.Substring(8).Split("--");
        System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ParseRepoIdFromDir: split into {parts.Length} parts");

        if (parts.Length != 2)
        {
            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ParseRepoIdFromDir: expected 2 parts (org/name), got {parts.Length}, returning null");
            return null;
        }

        var repoId = $"{parts[0]}/{parts[1]}";
        System.Diagnostics.Debug.WriteLine($"[HFCacheReader] ParseRepoIdFromDir: successfully parsed repoId={repoId}");
        return repoId;
    }

    private long CalculateModelSize(string revisionDir)
    {
        long totalSize = 0;
        System.Diagnostics.Debug.WriteLine($"[HFCacheReader] CalculateModelSize: calculating size for {revisionDir}");

        try
        {
            if (!Directory.Exists(revisionDir))
            {
                System.Diagnostics.Debug.WriteLine($"[HFCacheReader] CalculateModelSize: revision directory does not exist");
                return 0;
            }

            // CRITICAL FIX #1: Use cycle detection to prevent circular symlink hangs
            var visitedPaths = new HashSet<string>();
            var files = EnumerateFilesWithCycleDetection(revisionDir, visitedPaths);
            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] CalculateModelSize: found {files.Count} files");

            foreach (var file in files)
            {
                try
                {
                    var info = new FileInfo(file);
                    totalSize += info.Length;
                    System.Diagnostics.Debug.WriteLine($"[HFCacheReader] CalculateModelSize: file {Path.GetFileName(file)} is {info.Length} bytes");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HFCacheReader] CalculateModelSize: error reading file size for {file}: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] CalculateModelSize: total size={totalSize} bytes");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] CalculateModelSize: error calculating model size: {ex.Message}");
        }

        return totalSize;
    }

    private DateTime GetLastModified(string revisionDir)
    {
        System.Diagnostics.Debug.WriteLine($"[HFCacheReader] GetLastModified: getting last modified for {revisionDir}");

        try
        {
            if (!Directory.Exists(revisionDir))
            {
                System.Diagnostics.Debug.WriteLine($"[HFCacheReader] GetLastModified: revision directory does not exist, returning UtcNow");
                return DateTime.UtcNow;
            }

            // CRITICAL FIX #1: Use cycle detection to prevent circular symlink hangs
            var visitedPaths = new HashSet<string>();
            var files = EnumerateFilesWithCycleDetection(revisionDir, visitedPaths);
            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] GetLastModified: found {files.Count} files to check");

            if (files.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[HFCacheReader] GetLastModified: no files found, returning UtcNow");
                return DateTime.UtcNow;
            }

            var lastModified = files
                .Select(f => new FileInfo(f).LastWriteTimeUtc)
                .OrderByDescending(d => d)
                .First();

            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] GetLastModified: last modified={lastModified:o}");
            return lastModified;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] GetLastModified: error getting last modified time: {ex.Message}");
            return DateTime.UtcNow;
        }
    }

    /// <summary>
    /// CRITICAL FIX #1: Enumerates files recursively with cycle detection to prevent circular symlink hangs.
    /// </summary>
    private List<string> EnumerateFilesWithCycleDetection(string directory, HashSet<string> visitedPaths)
    {
        var files = new List<string>();

        try
        {
            // Get the full canonical path to detect cycles
            var fullPath = Path.GetFullPath(directory);

            // Check if this path is already being visited (cycle detected)
            if (visitedPaths.Contains(fullPath))
            {
                System.Diagnostics.Debug.WriteLine($"[HFCacheReader] EnumerateFilesWithCycleDetection: cycle detected at {fullPath}, skipping");
                return files;
            }

            visitedPaths.Add(fullPath);

            // Enumerate files in current directory only (non-recursive)
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    files.Add(file);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HFCacheReader] EnumerateFilesWithCycleDetection: permission denied for directory {directory}: {ex.Message}");
                return files;
            }

            // Recursively enumerate subdirectories with cycle detection
            try
            {
                foreach (var subdir in Directory.EnumerateDirectories(directory))
                {
                    // Skip .git directories
                    if (Path.GetFileName(subdir) == ".git")
                    {
                        System.Diagnostics.Debug.WriteLine($"[HFCacheReader] EnumerateFilesWithCycleDetection: skipping .git directory at {subdir}");
                        continue;
                    }

                    files.AddRange(EnumerateFilesWithCycleDetection(subdir, visitedPaths));
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HFCacheReader] EnumerateFilesWithCycleDetection: permission denied enumerating subdirectories of {directory}: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] EnumerateFilesWithCycleDetection: error enumerating files: {ex.Message}");
        }

        return files;
    }

    /// <summary>
    /// CRITICAL FIX #2: Validates that a path is within the cache directory (prevents directory escape attacks).
    /// </summary>
    private bool IsPathWithinCache(string path)
    {
        try
        {
            var fullCachePath = Path.GetFullPath(_cacheDir);
            var fullPath = Path.GetFullPath(path);

            // Ensure resolved path is within cache directory
            if (!fullPath.StartsWith(fullCachePath + Path.DirectorySeparatorChar) &&
                fullPath != fullCachePath)
            {
                System.Diagnostics.Debug.WriteLine($"[HFCacheReader] IsPathWithinCache: path {path} resolves outside cache (full: {fullPath}, cache: {fullCachePath})");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] IsPathWithinCache: path {path} is valid within cache");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HFCacheReader] IsPathWithinCache: error validating path {path}: {ex.Message}");
            return false;
        }
    }
}
