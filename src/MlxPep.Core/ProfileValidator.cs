namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        if (profile == null)
        {
            return new ValidationResult(false, new List<string> { "profile is required" });
        }

        var errors = new List<string>();
        var warnings = new List<string>();

        System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Beginning validation for profile: {profile.Id}");

        // Required fields
        if (profile.SchemaVersion != 1)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] schemaVersion check failed: expected 1, got {profile.SchemaVersion}");
            errors.Add($"schemaVersion must be 1, got {profile.SchemaVersion}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] schemaVersion check passed: {profile.SchemaVersion}");
        }

        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] id check failed: id is required and empty");
            errors.Add("id is required");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] id check passed: {profile.Id}");
        }

        if (string.IsNullOrWhiteSpace(profile.ModelHfId))
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] modelHfId check failed: modelHfId is required and empty");
            errors.Add("modelHfId is required");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] modelHfId check passed: {profile.ModelHfId}");
        }

        if (string.IsNullOrWhiteSpace(profile.Tier))
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] tier check failed: tier is required and empty");
            errors.Add("tier is required");
        }
        else if (!IsValidTier(profile.Tier))
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] tier check failed: '{profile.Tier}' is not valid (must be 'high', 'balanced', or 'efficient')");
            errors.Add($"tier must be 'high', 'balanced', or 'efficient', got '{profile.Tier}'");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] tier check passed: {profile.Tier}");
        }

        if (string.IsNullOrWhiteSpace(profile.Engine))
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] engine check failed: engine is required and empty");
            errors.Add("engine is required");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] engine check passed: {profile.Engine}");
        }

        // Validate required nested objects
        if (profile.Provenance == null)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] provenance check failed: provenance is null");
            errors.Add("provenance is required");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] provenance check passed: object present");
            if (string.IsNullOrWhiteSpace(profile.Provenance.Author))
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileValidator] provenance.author check failed: empty");
                errors.Add("provenance.author is required");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileValidator] provenance.author check passed: {profile.Provenance.Author}");
            }

            if (string.IsNullOrWhiteSpace(profile.Provenance.CreatedAt))
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileValidator] provenance.createdAt check failed: empty");
                errors.Add("provenance.createdAt is required");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileValidator] provenance.createdAt check passed: {profile.Provenance.CreatedAt}");
            }

            if (string.IsNullOrWhiteSpace(profile.Provenance.Source))
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileValidator] provenance.source check failed: empty");
                errors.Add("provenance.source is required");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileValidator] provenance.source check passed: {profile.Provenance.Source}");
            }
        }

        if (profile.Hardware == null)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] hardware check failed: hardware is null");
            errors.Add("hardware is required");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] hardware check passed: object present");
        }

        // Validate unknown keys with warnings (forward compatibility)
        if (profile.System != null)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Checking {profile.System.Count} system keys");
            foreach (var key in profile.System.Keys)
            {
                if (!KnownSystemKeys.Contains(key))
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Unknown system key found: '{key}'");
                    warnings.Add($"Unknown key in system: '{key}' (may be from a newer version)");
                }
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] system is null, skipping unknown key check");
        }

        if (profile.OMLXSettings != null)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Checking {profile.OMLXSettings.Count} OMLX keys");
            foreach (var key in profile.OMLXSettings.Keys)
            {
                if (!KnownOMLXKeys.Contains(key))
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Unknown OMLX key found: '{key}'");
                    warnings.Add($"Unknown key in omlx: '{key}' (may be from a newer version)");
                }
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] omlxSettings is null, skipping unknown key check");
        }

        if (profile.Harness != null)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Checking {profile.Harness.Count} harness keys");
            foreach (var key in profile.Harness.Keys)
            {
                if (!KnownHarnessKeys.Contains(key))
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Unknown harness key found: '{key}'");
                    warnings.Add($"Unknown key in harness: '{key}' (may be from a newer version)");
                }
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] harness is null, skipping unknown key check");
        }

        // Validate sampler settings bounds
        if (profile.Sampler != null)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Validating sampler settings");

            if (profile.Sampler.Temperature.HasValue)
            {
                var temp = profile.Sampler.Temperature.Value;
                if (temp < 0 || temp > 2)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Sampler temperature out of range: {temp}");
                    errors.Add($"Sampler temperature must be in range [0, 2], got {temp}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Sampler temperature valid: {temp}");
                }
            }

            if (profile.Sampler.TopP.HasValue)
            {
                var topP = profile.Sampler.TopP.Value;
                if (topP < 0 || topP > 1)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Sampler topP out of range: {topP}");
                    errors.Add($"Sampler topP must be in range [0, 1], got {topP}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Sampler topP valid: {topP}");
                }
            }

            if (profile.Sampler.TopK.HasValue)
            {
                var topK = profile.Sampler.TopK.Value;
                if (topK <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Sampler topK invalid (must be positive): {topK}");
                    errors.Add($"Sampler topK must be positive, got {topK}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Sampler topK valid: {topK}");
                }
            }

            if (profile.Sampler.RepetitionPenalty.HasValue)
            {
                var repPenalty = profile.Sampler.RepetitionPenalty.Value;
                if (repPenalty < 0 || repPenalty > 2)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Sampler repetitionPenalty out of range: {repPenalty}");
                    errors.Add($"Sampler repetitionPenalty must be in range [0, 2], got {repPenalty}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Sampler repetitionPenalty valid: {repPenalty}");
                }
            }

            if (profile.Sampler.ContextTokens.HasValue)
            {
                var contextTokens = profile.Sampler.ContextTokens.Value;
                if (contextTokens <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Sampler contextTokens invalid (must be positive): {contextTokens}");
                    errors.Add($"Sampler contextTokens must be positive, got {contextTokens}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Sampler contextTokens valid: {contextTokens}");
                }
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] sampler is null, skipping sampler validation");
        }

        var result = errors.Any()
            ? new ValidationResult(false, errors, warnings)
            : new ValidationResult(true, new List<string>(), warnings);

        System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Validation complete for profile {profile.Id}: Valid={result.IsValid}, Errors={result.Errors.Count}, Warnings={result.Warnings.Count}");
        return result;
    }

    /// <summary>
    /// Validates a set of profiles loaded from JSONL, ensuring tier uniqueness.
    /// </summary>
    public ValidationResult ValidateProfileSet(List<Profile> profiles)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Beginning profile set validation: {profiles.Count} profiles");

        if (!profiles.Any())
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Profile set is empty, validation passed");
            return new ValidationResult(true, new List<string>(), new List<string>());
        }

        // Check for tier uniqueness
        System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Checking tier uniqueness across {profiles.Count} profiles");
        var tierCounts = profiles
            .GroupBy(p => p.Tier, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var (tier, count) in tierCounts)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Tier '{tier}' appears {count} time(s)");
            if (count > 1)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Tier uniqueness check failed: '{tier}' appears {count} times (expected exactly 1)");
                errors.Add($"Tier '{tier}' appears {count} times in the profile set. Each tier must appear exactly once.");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Tier '{tier}' uniqueness check passed");
            }
        }

        // Validate each profile
        System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Validating individual profiles in set");
        foreach (var profile in profiles)
        {
            var result = ValidateForLocalUse(profile);
            if (!result.IsValid)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Profile '{profile.Id}' validation failed with {result.Errors.Count} errors");
                errors.AddRange(result.Errors.Select(e => $"Profile '{profile.Id}': {e}"));
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Profile '{profile.Id}' validation passed");
            }
            if (result.Warnings.Any())
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Profile '{profile.Id}' has {result.Warnings.Count} warnings");
            }
            warnings.AddRange(result.Warnings);
        }

        var setValid = !errors.Any();
        System.Diagnostics.Debug.WriteLine($"[ProfileValidator] Profile set validation complete: Valid={setValid}, TotalErrors={errors.Count}, TotalWarnings={warnings.Count}");

        return setValid
            ? new ValidationResult(true, new List<string>(), warnings)
            : new ValidationResult(false, errors, warnings);
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
