namespace MlxPep.Core.Python;

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Manages Python environment setup and model-assessor integration.
/// Ensures model-assessor scripts and dependencies are available at runtime.
/// </summary>
public class PythonEnvironmentManager
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string? ModelAssessorPath = ResolveModelAssessorPath();

    /// <summary>
    /// Verifies model-assessor is available and accessible.
    /// Returns true if scripts directory exists and contains expected files.
    /// </summary>
    public static bool IsModelAssessorAvailable()
    {
        var available = ModelAssessorPath != null && Directory.Exists(GetModelAssessorScriptsPath());
        Debug.WriteLine($"[PythonEnvironmentManager] Model-assessor availability: {available}");
        return available;
    }

    /// <summary>
    /// Gets the path to the model-assessor scripts directory.
    /// Used by ProfilingRunner to locate and invoke assessment scripts.
    /// </summary>
    public static string GetModelAssessorScriptsPath()
    {
        return Path.Combine(GetModelAssessorRootPath(), "scripts");
    }

    /// <summary>
    /// Gets the path to model-assessor root directory.
    /// </summary>
    public static string GetModelAssessorRootPath()
    {
        if (ModelAssessorPath != null)
        {
            Debug.WriteLine($"[PythonEnvironmentManager] Using model-assessor root at {ModelAssessorPath}");
            return ModelAssessorPath;
        }

        var candidates = string.Join(Environment.NewLine, GetCandidateModelAssessorPaths().Select(path => $"  - {path}"));
        throw new InvalidOperationException(
            "Cannot locate model-assessor. Checked the following paths:" + Environment.NewLine + candidates);
    }

    /// <summary>
    /// Finds the repository root by walking up from the current assembly location.
    /// Stops at the first directory containing .git or mlx-pep.slnx.
    /// </summary>
    private static string FindRepoRoot()
    {
        var currentDir = new FileInfo(typeof(PythonEnvironmentManager).Assembly.Location).DirectoryName;
        
        if (currentDir == null)
            throw new InvalidOperationException("Cannot determine assembly location");

        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir, ".git")) ||
                File.Exists(Path.Combine(currentDir, "mlx-pep.slnx")))
            {
                return currentDir;
            }

            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        throw new InvalidOperationException("Cannot find repository root (mlx-pep.slnx or .git not found)");
    }

    private static string? ResolveModelAssessorPath()
    {
        foreach (var candidatePath in GetCandidateModelAssessorPaths())
        {
            var scriptsPath = Path.Combine(candidatePath, "scripts");
            Debug.WriteLine($"[PythonEnvironmentManager] Checking model-assessor candidate: {candidatePath}");
            if (Directory.Exists(candidatePath) && Directory.Exists(scriptsPath))
            {
                Debug.WriteLine($"[PythonEnvironmentManager] Found model-assessor at {candidatePath}");
                return candidatePath;
            }
        }

        Debug.WriteLine("[PythonEnvironmentManager] No model-assessor candidate path was usable");
        return null;
    }

    private static IEnumerable<string> GetCandidateModelAssessorPaths()
    {
        var configuredPath = Environment.GetEnvironmentVariable("MLX_PEP_MODEL_ASSESSOR_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            yield return ExpandHomeDirectory(configuredPath);
        }

        yield return Path.Combine(RepoRoot, "src", "model-assessor");

        var repoParent = Directory.GetParent(RepoRoot)?.FullName;
        if (!string.IsNullOrWhiteSpace(repoParent))
        {
            yield return Path.Combine(repoParent, "model-assessor");
        }

        var assemblyDirectory = new FileInfo(typeof(PythonEnvironmentManager).Assembly.Location).DirectoryName;
        if (!string.IsNullOrWhiteSpace(assemblyDirectory))
        {
            yield return Path.Combine(assemblyDirectory, "model-assessor");
        }
    }

    private static string ExpandHomeDirectory(string path)
    {
        if (path.StartsWith("~/", StringComparison.Ordinal))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                path[2..]);
        }

        return path;
    }
}
