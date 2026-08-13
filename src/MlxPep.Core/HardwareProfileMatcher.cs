namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Hardware-based profile discovery and compatibility matching.
/// Issue #8: Core foundation - basic hardware matching
///
/// Enables profiles to be discoverable by similar hardware, supporting:
/// - Exact chip matching (e.g., "Apple M4 Max")
/// - Hardware family matching (e.g., "Apple Silicon", "GPU Cluster")
///
/// Note: Issue #27 (publish-flow polish) will extend this with memory ranges and community metadata.
/// </summary>
public class HardwareProfileMatcher
{
    /// <summary>
    /// Represents a hardware compatibility score.
    /// </summary>
    public record HardwareMatch(Profile Profile, double Score, string Reason)
    {
        public override string ToString() => $"{Profile.Id} (score: {Score:P0}) - {Reason}";
    }

    /// <summary>
    /// Hardware family classifications.
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> HardwareFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Apple Silicon", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Apple M1", "Apple M2", "Apple M3", "Apple M4", "Apple M4 Pro", "Apple M4 Max", "Apple M1 Pro", "Apple M1 Max", "Apple M2 Pro", "Apple M2 Max", "Apple M3 Pro", "Apple M3 Max" } },

        { "Intel", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Intel Core i5", "Intel Core i7", "Intel Core i9", "Intel Xeon", "Intel Core Ultra" } },

        { "AMD", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "AMD Ryzen", "AMD EPYC", "Ryzen" } },

        { "GPU Cluster", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "A100", "H100", "L40S", "RTX 6000 Ada", "RTX 5880 Ada", "V100", "A40" } },

        { "Mobile GPU", new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "RTX 3090 Mobile", "RTX 4090 Mobile", "RTX 4080 Mobile", "RTX 4070 Mobile", "A6000 Mobile", "L40S" } }
    };

    /// <summary>
    /// Determines the hardware family of a chip.
    /// </summary>
    public string? DetermineHardwareFamily(string chip)
    {
        foreach (var (family, chips) in HardwareFamilies)
        {
            if (chips.Any(c => chip.Contains(c, StringComparison.OrdinalIgnoreCase)))
                return family;
        }

        return null;
    }

    /// <summary>
    /// Finds exact chip matches from a profile list.
    /// </summary>
    public List<HardwareMatch> FindExactChipMatches(List<Profile> profiles, string targetChip)
    {
        return profiles
            .Where(p => p.Hardware.Chip.Equals(targetChip, StringComparison.OrdinalIgnoreCase))
            .Select(p => new HardwareMatch(p, 1.0, $"Exact chip match: {targetChip}"))
            .ToList();
    }

    /// <summary>
    /// Finds profiles compatible with a target hardware configuration.
    /// Considers chip family and similar factors.
    /// </summary>
    public List<HardwareMatch> FindCompatibleProfiles(
        List<Profile> profiles,
        int targetMemoryGb,
        string targetChip,
        string? targetHardwareFamily = null)
    {
        var matches = new List<HardwareMatch>();
        var targetFamily = targetHardwareFamily ?? DetermineHardwareFamily(targetChip);

        foreach (var profile in profiles)
        {
            var profileFamily = DetermineHardwareFamily(profile.Hardware.Chip);
            var score = CalculateCompatibilityScore(
                profile: profile,
                targetChip: targetChip,
                targetFamily: targetFamily,
                profileFamily: profileFamily
            );

            if (score > 0)
            {
                var reason = BuildMatchReason(profile, targetChip);
                matches.Add(new HardwareMatch(profile, score, reason));
            }
        }

        // Sort by score descending (best matches first)
        return matches.OrderByDescending(m => m.Score).ToList();
    }

    /// <summary>
    /// Calculates a compatibility score (0.0 to 1.0) between target hardware and a profile.
    /// </summary>
    private double CalculateCompatibilityScore(
        Profile profile,
        string targetChip,
        string? targetFamily,
        string? profileFamily)
    {
        double score = 0.0;

        // Exact chip match: 1.0
        if (profile.Hardware.Chip.Equals(targetChip, StringComparison.OrdinalIgnoreCase))
            return 1.0;

        // Family match: 0.7
        if (targetFamily != null && profileFamily != null &&
            targetFamily.Equals(profileFamily, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.7;
        }
        else if (targetFamily == profileFamily)
        {
            score += 0.5;
        }

        // Normalize to 0.0-1.0 range
        return Math.Min(score, 1.0);
    }

    /// <summary>
    /// Builds a human-readable match reason string.
    /// </summary>
    private string BuildMatchReason(Profile profile, string targetChip)
    {
        var reasons = new List<string>();

        if (profile.Hardware.Chip.Equals(targetChip, StringComparison.OrdinalIgnoreCase))
            reasons.Add("exact chip match");
        else
            reasons.Add($"compatible with {targetChip}");

        return string.Join(", ", reasons);
    }

    /// <summary>
    /// Filters profiles to those that would work on the target hardware.
    /// More permissive than FindCompatibleProfiles - includes any profiles
    /// that don't explicitly exclude the target configuration.
    /// </summary>
    public List<Profile> FilterByHardwareCompatibility(
        List<Profile> profiles,
        int targetMemoryGb,
        string targetChip)
    {
        // For issue #8, just return all profiles that have compatible chip family
        var targetFamily = DetermineHardwareFamily(targetChip);
        return profiles.Where(p =>
        {
            var profileFamily = DetermineHardwareFamily(p.Hardware.Chip);
            // Allow exact match or family match
            return p.Hardware.Chip.Equals(targetChip, StringComparison.OrdinalIgnoreCase) ||
                   (targetFamily != null && profileFamily != null &&
                    targetFamily.Equals(profileFamily, StringComparison.OrdinalIgnoreCase));
        }).ToList();
    }
}
