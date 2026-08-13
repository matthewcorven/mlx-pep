namespace MlxPep.Core.Detectors;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Read-only detector for oMLX runtime state and configuration.
/// Issue #10: core: system + oMLX read-only detectors
///
/// Reads:
/// - ~/Library/Application Support/oMLX/config.json (base_path, port, model_dir)
/// - ~/Library/Application Support/oMLX/logs/server.log (latest guard tier, ceiling, metal cap, wired_limit)
///
/// Scans log in reverse order to find latest values efficiently.
/// Never mutates oMLX state.
/// </summary>
public class OmlxDetector
{
    private static readonly string OmlxConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "oMLX",
        "config.json"
    );

    private static readonly string OmlxLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "oMLX",
        "logs",
        "server.log"
    );

    /// <summary>
    /// Detects oMLX runtime state.
    /// Returns graceful defaults for missing data rather than throwing.
    /// </summary>
    public OmlxState Detect()
    {
        try
        {
            var config = ReadOmlxConfig();
            var (guardTier, ceilingGb, metalCapGb, recommendedWiredLimitMb) = ParseOmlxLog();

            return new OmlxState(
                ConfigPath: OmlxConfigPath,
                LogPath: OmlxLogPath,
                BasePath: config.ContainsKey("base_path") ? config["base_path"]?.ToString() : null,
                Port: config.ContainsKey("port") && config["port"] is JsonElement portElem && portElem.TryGetInt32(out var portVal)
                    ? portVal
                    : null,
                ModelDir: config.ContainsKey("model_dir") ? config["model_dir"]?.ToString() : null,
                CurrentMemoryGuardTier: guardTier,
                CurrentCeilingGb: ceilingGb,
                CurrentMetalCapGb: metalCapGb,
                RecommendedWiredLimitMb: recommendedWiredLimitMb
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OmlxDetector error: {ex.Message}");
            return new OmlxState(
                ConfigPath: OmlxConfigPath,
                LogPath: OmlxLogPath,
                BasePath: null,
                Port: null,
                ModelDir: null,
                CurrentMemoryGuardTier: "unknown",
                CurrentCeilingGb: null,
                CurrentMetalCapGb: null,
                RecommendedWiredLimitMb: null
            );
        }
    }

    /// <summary>
    /// Reads and parses oMLX config.json.
    /// Returns empty dict if file doesn't exist or can't be parsed.
    /// </summary>
    private static Dictionary<string, object?> ReadOmlxConfig()
    {
        if (!File.Exists(OmlxConfigPath))
            return new Dictionary<string, object?>();

        try
        {
            var jsonText = File.ReadAllText(OmlxConfigPath);
            using var doc = JsonDocument.Parse(jsonText);
            var result = new Dictionary<string, object?>();

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result[prop.Name] = prop.Value;
            }

            return result;
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    /// <summary>
    /// Parses oMLX server.log in reverse to find latest:
    /// - Memory guard tier: "Memory guard tier: [a-z]+"
    /// - Ceiling: "ceiling=[\d.]+GB"
    /// - Metal cap: "Metal cap \(([\d.]+)GB"
    /// - Recommended wired limit: "iogpu\.wired_limit_mb=\d+"
    ///
    /// Returns (tier, ceilingGb, metalCapGb, wiredLimitMb) with nulls/defaults for missing values.
    /// </summary>
    private static (string tier, double? ceilingGb, double? metalCapGb, int? wiredLimitMb) ParseOmlxLog()
    {
        if (!File.Exists(OmlxLogPath))
            return ("unknown", null, null, null);

        try
        {
            var lines = File.ReadAllLines(OmlxLogPath);
            string? guardTier = null;
            double? ceilingGb = null;
            double? metalCapGb = null;
            int? wiredLimitMb = null;

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i];

                if (guardTier == null)
                {
                    var m = Regex.Match(line, @"Memory guard tier:\s+([a-z]+)", RegexOptions.IgnoreCase);
                    if (m.Success)
                        guardTier = m.Groups[1].Value.ToLowerInvariant();
                }

                if (ceilingGb == null)
                {
                    var m = Regex.Match(line, @"ceiling=([\d.]+)GB", RegexOptions.IgnoreCase);
                    if (m.Success && double.TryParse(m.Groups[1].Value, out var val))
                        ceilingGb = val;
                }

                if (metalCapGb == null)
                {
                    var m = Regex.Match(line, @"Metal cap \(([\d.]+)GB", RegexOptions.IgnoreCase);
                    if (m.Success && double.TryParse(m.Groups[1].Value, out var val))
                        metalCapGb = val;
                }

                if (wiredLimitMb == null)
                {
                    var m = Regex.Match(line, @"iogpu\.wired_limit_mb=(\d+)", RegexOptions.IgnoreCase);
                    if (m.Success && int.TryParse(m.Groups[1].Value, out var val))
                        wiredLimitMb = val;
                }

                if (guardTier != null && ceilingGb != null && metalCapGb != null && wiredLimitMb != null)
                    break;
            }

            return (
                tier: guardTier ?? "unknown",
                ceilingGb: ceilingGb,
                metalCapGb: metalCapGb,
                wiredLimitMb: wiredLimitMb
            );
        }
        catch
        {
            return ("unknown", null, null, null);
        }
    }
}
