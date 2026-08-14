namespace MlxPep.Core.Diagnostics;

/// <summary>
/// Abstraction for probing system dependencies.
/// Enables testing via mocked implementations.
/// </summary>
public interface IDependencyProbe
{
    /// <summary>
    /// Probe the system for a dependency.
    /// Returns raw output from the probe (stdout, file path, etc.).
    /// </summary>
    Task<ProbeResult> ProbeAsync();

    /// <summary>
    /// Parse version information from the probe's raw output.
    /// Returns null if version cannot be determined.
    /// </summary>
    string? ParseVersion(string rawOutput);
}

/// <summary>
/// Result of a dependency probe.
/// </summary>
public class ProbeResult
{
    /// <summary>
    /// Whether the dependency was found/detected.
    /// </summary>
    public bool Found { get; set; }

    /// <summary>
    /// Raw output from the probe (stdout, file path, etc.).
    /// Used for version parsing and debugging.
    /// </summary>
    public string? RawOutput { get; set; }

    /// <summary>
    /// Error message if probe failed (e.g., process execution error).
    /// Empty/null if probe succeeded (even if dependency not found).
    /// </summary>
    public string? Error { get; set; }
}
