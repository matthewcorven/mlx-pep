namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Validates profiles for local use.
/// Issue #8: core: profile schema records + STJ source-gen + JSONL validation
///
/// Validation rules:
/// - schemaVersion must be 1
/// - id, modelHfId, tier, engine must be non-empty
/// - Tiers in a profile set must be unique (high, balanced, efficient)
/// - Unknown keys in system/omlx/harness: log warning (forward compatibility)
/// - Known keys are validated against allowlist
/// </summary>
public class ProfileValidator
{
    private static readonly HashSet<string> KnownSystemKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "iogpu.wired_limit_mb",
        "memory_cache_mb",
        "antml.max_model_size_mb",
        "antml.npu_timeout",
        "antml.allow_remote_execution",
        "gpu_memory_fraction"
    };

    private static readonly HashSet<string> KnownOMLXKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "memory_guard_tier",
        "memory_guard_ceiling_gb",
        "thread_limit",
        "quantization",
        "enable_multi_gpu",
        "compute_units",
        "model_dtype",
        "batch_size"
    };

    private static readonly HashSet<string> KnownHarnessKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "vscode",
        "copilotCli",
        "claudeCode",
        "opencode"
    };

    /// <summary>
    /// Validates a profile for local use only.
    /// </summary>
    public ValidationResult ValidateForLocalUse(Profile profile)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Required fields
        if (profile.SchemaVersion != 1)
            errors.Add($"schemaVersion must be 1, got {profile.SchemaVersion}");

        if (string.IsNullOrWhiteSpace(profile.Id))
            errors.Add("id is required");

        if (string.IsNullOrWhiteSpace(profile.ModelHfId))
            errors.Add("modelHfId is required");

        if (string.IsNullOrWhiteSpace(profile.Tier))
            errors.Add("tier is required");
        else if (!IsValidTier(profile.Tier))
            errors.Add($"tier must be 'high', 'balanced', or 'efficient', got '{profile.Tier}'");

        if (string.IsNullOrWhiteSpace(profile.Engine))
            errors.Add("engine is required");

        // Validate required nested objects
        if (profile.Provenance == null)
            errors.Add("provenance is required");
        else
        {
            if (string.IsNullOrWhiteSpace(profile.Provenance.Author))
                errors.Add("provenance.author is required");
            if (string.IsNullOrWhiteSpace(profile.Provenance.CreatedAt))
                errors.Add("provenance.createdAt is required");
            if (string.IsNullOrWhiteSpace(profile.Provenance.Source))
                errors.Add("provenance.source is required");
        }

        if (profile.Hardware == null)
            errors.Add("hardware is required");

        // Validate unknown keys with warnings (forward compatibility)
        if (profile.System != null)
        {
            foreach (var key in profile.System.Keys)
            {
                if (!KnownSystemKeys.Contains(key))
                    warnings.Add($"Unknown key in system: '{key}' (may be from a newer version)");
            }
        }

        if (profile.OMLXSettings != null)
        {
            foreach (var key in profile.OMLXSettings.Keys)
            {
                if (!KnownOMLXKeys.Contains(key))
                    warnings.Add($"Unknown key in omlx: '{key}' (may be from a newer version)");
            }
        }

        if (profile.Harness != null)
        {
            foreach (var key in profile.Harness.Keys)
            {
                if (!KnownHarnessKeys.Contains(key))
                    warnings.Add($"Unknown key in harness: '{key}' (may be from a newer version)");
            }
        }

        return errors.Any()
            ? new ValidationResult(false, errors, warnings)
            : new ValidationResult(true, new List<string>(), warnings);
    }

    /// <summary>
    /// Validates a set of profiles loaded from JSONL, ensuring tier uniqueness.
    /// </summary>
    public ValidationResult ValidateProfileSet(List<Profile> profiles)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (!profiles.Any())
            return new ValidationResult(true, new List<string>(), new List<string>());

        // Check for tier uniqueness
        var tierCounts = profiles
            .GroupBy(p => p.Tier, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var (tier, count) in tierCounts)
        {
            if (count > 1)
                errors.Add($"Tier '{tier}' appears {count} times in the profile set. Each tier must appear exactly once.");
        }

        // Validate each profile
        foreach (var profile in profiles)
        {
            var result = ValidateForLocalUse(profile);
            if (!result.IsValid)
                errors.AddRange(result.Errors.Select(e => $"Profile '{profile.Id}': {e}"));
            warnings.AddRange(result.Warnings);
        }

        return errors.Any()
            ? new ValidationResult(false, errors, warnings)
            : new ValidationResult(true, new List<string>(), warnings);
    }

    private static bool IsValidTier(string tier) => tier switch
    {
        "high" or "balanced" or "efficient" => true,
        _ => false
    };
}

/// <summary>
/// Result of profile validation.
/// </summary>
public record ValidationResult(
    bool IsValid,
    List<string> Errors,
    List<string> Warnings = null!)
{
    public ValidationResult(bool isValid, List<string> errors) : this(isValid, errors, new List<string>()) { }
}
