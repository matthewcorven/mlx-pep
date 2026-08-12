namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Validates profiles for local use or publishing.
/// Issue #27: profiling: publish-flow polish + community metadata
/// </summary>
public class ProfileValidator
{
    private static readonly HashSet<string> ValidTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "production", "experimental", "benchmark", "inference", "training",
        "quantized", "unquantized", "streaming", "cpu", "gpu", "npu",
        "high-latency", "low-latency", "high-throughput", "memory-optimized",
        "speed-optimized", "accuracy-optimized"
    };

    private readonly RuntimeEngineRegistry _engineRegistry;

    public ProfileValidator(RuntimeEngineRegistry? engineRegistry = null)
    {
        _engineRegistry = engineRegistry ?? new RuntimeEngineRegistry();
    }

    /// <summary>
    /// Validates a profile for local use only.
    /// </summary>
    public ValidationResult ValidateForLocalUse(Profile profile)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(profile.Id))
            errors.Add("Profile ID is required.");

        if (string.IsNullOrWhiteSpace(profile.ModelHfId))
            errors.Add("Model HuggingFace ID is required.");

        if (string.IsNullOrWhiteSpace(profile.Engine))
            errors.Add("Engine is required.");
        else if (!_engineRegistry.IsSupported(profile.Engine))
            errors.Add($"Unsupported engine '{profile.Engine}'. Supported engines: {string.Join(", ", _engineRegistry.GetEngineIds())}");

        return errors.Any()
            ? new ValidationResult(false, errors)
            : new ValidationResult(true, new List<string>());
    }

    /// <summary>
    /// Validates a profile for publishing to the community repository.
    /// Requires community metadata and stricter validation.
    /// </summary>
    public ValidationResult ValidateForPublishing(Profile profile)
    {
        var errors = new List<string>();

        // First, validate local requirements
        var localValidation = ValidateForLocalUse(profile);
        if (!localValidation.IsValid)
            return localValidation;

        // Require community metadata
        if (profile.Community == null)
        {
            errors.Add("Community metadata is required for publishing.");
            return new ValidationResult(false, errors);
        }

        var community = profile.Community;

        // Validate dedupKey (required for publishing)
        if (string.IsNullOrWhiteSpace(community.DedupKey))
            errors.Add("Deduplication key (dedupKey) is required for publishing.");
        else if (!IsValidDedupKey(community.DedupKey))
            errors.Add("Deduplication key must be alphanumeric with hyphens, 3-50 characters.");

        // Validate memory range if specified
        if (community.MinMemoryGb.HasValue && community.MaxMemoryGb.HasValue)
        {
            if (community.MinMemoryGb > community.MaxMemoryGb)
                errors.Add("Minimum memory cannot exceed maximum memory.");
        }

        // Validate description length
        if (!string.IsNullOrWhiteSpace(community.Description) && community.Description.Length > 500)
            errors.Add("Description cannot exceed 500 characters.");

        // Validate tags
        if (community.Tags?.Any() == true)
        {
            var invalidTags = community.Tags.Where(t => !ValidTags.Contains(t)).ToList();
            if (invalidTags.Any())
                errors.Add($"Invalid tags: {string.Join(", ", invalidTags)}. Valid tags: {string.Join(", ", ValidTags)}");
        }

        return errors.Any()
            ? new ValidationResult(false, errors)
            : new ValidationResult(true, new List<string>());
    }

    /// <summary>
    /// Validates engine-specific settings using the appropriate runtime engine handler.
    /// </summary>
    public ValidationResult ValidateEngineSettings(Profile profile)
    {
        return _engineRegistry.ValidateProfileForEngine(profile);
    }

    private static bool IsValidDedupKey(string key)
    {
        if (key.Length < 3 || key.Length > 50)
            return false;

        return key.All(c => char.IsLetterOrDigit(c) || c == '-');
    }
}

/// <summary>
/// Result of profile validation.
/// </summary>
public record ValidationResult(bool IsValid, List<string> Errors);
