namespace MlxPep.Core.Diagnostics;

/// <summary>
/// Complete dependency detection report for all tools.
/// </summary>
public class DependencyReport
{
    /// <summary>
    /// Timestamp when the report was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Overall status of dependency detection.
    /// </summary>
    public DependencyReportStatus Status { get; set; } = DependencyReportStatus.Success;

    /// <summary>
    /// Detailed status for each detected dependency.
    /// Key: tool name (e.g., "dotnet", "hf-cli", "python3").
    /// </summary>
    public Dictionary<string, ToolStatus> Tools { get; set; } = new();

    /// <summary>
    /// Any errors encountered during detection (non-fatal).
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Status of a single dependency tool.
/// </summary>
public class ToolStatus
{
    /// <summary>
    /// Tool identifier (e.g., "dotnet", "hf-cli").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Display name for CLI output (e.g., ".NET").
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// Whether the tool is installed/available.
    /// </summary>
    public bool Installed { get; set; }

    /// <summary>
    /// Version string if available (e.g., "10.0.0").
    /// Null if version cannot be determined.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Scope of installation: "user", "global", or "unknown".
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Human-readable message about the tool status.
    /// Used when tool is not installed or error occurred.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Installation guidance for this tool in current environment.
    /// </summary>
    public string? InstallGuidance { get; set; }

    /// <summary>
    /// Raw output from the probe (for debugging).
    /// </summary>
    public string? RawOutput { get; set; }

    /// <summary>
    /// Path to the tool if it was detected on the file system.
    /// </summary>
    public string? ToolPath { get; set; }
}

/// <summary>
/// Overall status of dependency detection.
/// </summary>
public enum DependencyReportStatus
{
    /// <summary>
    /// Detection completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// Detection encountered non-fatal errors (see Warnings).
    /// </summary>
    PartialSuccess,

    /// <summary>
    /// Detection failed completely.
    /// </summary>
    Failed
}
