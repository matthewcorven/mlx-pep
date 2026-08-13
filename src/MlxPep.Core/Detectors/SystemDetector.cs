namespace MlxPep.Core.Detectors;

using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

/// <summary>
/// Read-only detector for macOS system hardware information.
/// Issue #10: core: system + oMLX read-only detectors
///
/// Reads system configuration via subprocess calls to:
/// - system_profiler SPHardwareDataType SPStorageDataType (model, chip, memory, storage)
/// - sysctl iogpu.wired_limit_mb (GPU wired memory limit)
///
/// Never mutates system state.
/// </summary>
public class SystemDetector
{
    /// <summary>
    /// Detects system hardware information.
    /// Returns graceful defaults for missing data rather than throwing.
    /// </summary>
    public SystemHardwareInfo Detect()
    {
        try
        {
            var hwText = RunCommand("system_profiler", "SPHardwareDataType", "SPStorageDataType");
            var wiringLimitText = RunCommand("sysctl", "iogpu.wired_limit_mb");

            var modelName = ExtractMatch(hwText, @"Model Name:\s+(.+)", "Unknown");
            var modelIdentifier = ExtractMatch(hwText, @"Model Identifier:\s+(.+)", "Unknown");
            var chip = ExtractMatch(hwText, @"Chip:\s+(.+)", "Unknown");
            var memoryGb = ExtractIntMatch(hwText, @"Memory:\s+(\d+)\s+GB", 0);
            var storageFreeGb = ExtractDoubleMatch(hwText, @"Free:\s+([\d.]+)\s+GB");
            var storageCapacityTb = ExtractIntMatch(hwText, @"Capacity:\s+(\d+)\s+TB");
            var wiredLimitMb = ExtractIntMatch(wiringLimitText, @"iogpu\.wired_limit_mb:\s+(\d+)", 0);

            return new SystemHardwareInfo(
                ModelName: modelName,
                ModelIdentifier: modelIdentifier,
                Chip: chip,
                MemoryGb: memoryGb,
                StorageFreeGb: storageFreeGb,
                StorageCapacityTb: storageCapacityTb > 0 ? storageCapacityTb : null,
                WiredLimitMb: wiredLimitMb
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SystemDetector error: {ex.Message}");
            return new SystemHardwareInfo(
                ModelName: "Unknown",
                ModelIdentifier: "Unknown",
                Chip: "Unknown",
                MemoryGb: 0,
                StorageFreeGb: null,
                StorageCapacityTb: null,
                WiredLimitMb: 0
            );
        }
    }

    /// <summary>
    /// Executes a shell command and returns stdout.
    /// Returns empty string on failure.
    /// </summary>
    private static string RunCommand(params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = args[0],
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            for (int i = 1; i < args.Length; i++)
            {
                psi.ArgumentList.Add(args[i]);
            }

            using var proc = Process.Start(psi);
            if (proc == null)
                return string.Empty;

            proc.WaitForExit(5000);
            var output = proc.StandardOutput.ReadToEnd();
            return proc.ExitCode == 0 ? output : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Extracts a string value using regex, returns default if no match.
    /// </summary>
    private static string ExtractMatch(string text, string pattern, string defaultValue)
    {
        try
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value.Trim() : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Extracts an integer value using regex, returns default if no match or parse fails.
    /// </summary>
    private static int ExtractIntMatch(string text, string pattern, int defaultValue = 0)
    {
        try
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Extracts a double value using regex, returns null if no match or parse fails.
    /// </summary>
    private static double? ExtractDoubleMatch(string text, string pattern)
    {
        try
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return match.Success && double.TryParse(match.Groups[1].Value, out var value) ? value : null;
        }
        catch
        {
            return null;
        }
    }
}
