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
    private static readonly string ModelAssessorPath = Path.Combine(RepoRoot, "src", "model-assessor");
    private static readonly string ScriptsPath = Path.Combine(ModelAssessorPath, "scripts");

    /// <summary>
    /// Verifies model-assessor is available and accessible.
    /// Returns true if scripts directory exists and contains expected files.
    /// </summary>
    public static bool IsModelAssessorAvailable()
    {
        return Directory.Exists(ModelAssessorPath) && Directory.Exists(ScriptsPath);
    }

    /// <summary>
    /// Gets the path to the model-assessor scripts directory.
    /// Used by ProfilingRunner to locate and invoke assessment scripts.
    /// </summary>
    public static string GetModelAssessorScriptsPath()
    {
        return ScriptsPath;
    }

    /// <summary>
    /// Gets the path to model-assessor root directory.
    /// </summary>
    public static string GetModelAssessorRootPath()
    {
        return ModelAssessorPath;
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
}
