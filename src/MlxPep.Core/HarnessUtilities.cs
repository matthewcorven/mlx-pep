namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Utility functions for harness operations.
/// </summary>
public static class HarnessUtilities
{
    /// <summary>
    /// Expands user home and environment variable paths.
    /// </summary>
    public static string ExpandPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        return path
            .Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
            .Replace("%APPDATA%", Environment.GetEnvironmentVariable("APPDATA") ?? "")
            .Replace("%USERPROFILE%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    /// <summary>
    /// Generates a unified diff output between two strings.
    /// </summary>
    public static string GenerateUnifiedDiff(string? existing, string? proposed, string filePath)
    {
        existing ??= "";
        proposed ??= "";

        if (existing == proposed)
            return $"[FILE] {filePath} (UNCHANGED)\n";

        var existingLines = existing.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        var proposedLines = proposed.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

        var sb = new StringBuilder();
        sb.AppendLine($"[FILE] {filePath} ({(string.IsNullOrEmpty(existing) ? "NEW" : "MODIFIED")})");
        sb.AppendLine("--- existing");
        sb.AppendLine("+++ proposed");
        sb.AppendLine($"@@ -1,{existingLines.Length} +1,{proposedLines.Length} @@");

        // Simple diff: show all lines (context-based diff would be more complex)
        var maxLines = Math.Max(existingLines.Length, proposedLines.Length);
        for (int i = 0; i < maxLines; i++)
        {
            var existingLine = i < existingLines.Length ? existingLines[i] : null;
            var proposedLine = i < proposedLines.Length ? proposedLines[i] : null;

            if (existingLine == proposedLine && !string.IsNullOrEmpty(existingLine))
            {
                sb.AppendLine($" {existingLine}");
            }
            else
            {
                if (!string.IsNullOrEmpty(existingLine))
                    sb.AppendLine($"-{existingLine}");
                if (!string.IsNullOrEmpty(proposedLine))
                    sb.AppendLine($"+{proposedLine}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the ISO-8601 timestamp with filesystem-safe formatting (dots instead of colons).
    /// </summary>
    public static string GetTimestampForBackup()
    {
        // Format: 2026-08-12T00.35.51Z (ISO-8601 with dots for filesystem safety)
        var now = DateTime.UtcNow;
        return now.ToString("yyyy-MM-ddTHH.mm.ssZ");
    }

    /// <summary>
    /// Deep merges source dictionary into target dictionary.
    /// </summary>
    public static void DeepMerge(Dictionary<string, object> target, Dictionary<string, object> source)
    {
        foreach (var kvp in source)
        {
            if (target.ContainsKey(kvp.Key))
            {
                if (kvp.Value is Dictionary<string, object> sourceDict &&
                    target[kvp.Key] is Dictionary<string, object> targetDict)
                {
                    DeepMerge(targetDict, sourceDict);
                }
                else
                {
                    target[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                target[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Recursively gets a value from nested dictionaries by dot-separated key path.
    /// </summary>
    public static object? GetNestedValue(Dictionary<string, object> dict, string keyPath)
    {
        var keys = keyPath.Split('.');
        object? current = dict;

        foreach (var key in keys)
        {
            if (current is Dictionary<string, object> d && d.TryGetValue(key, out var value))
            {
                current = value;
            }
            else
            {
                return null;
            }
        }

        return current;
    }
}
