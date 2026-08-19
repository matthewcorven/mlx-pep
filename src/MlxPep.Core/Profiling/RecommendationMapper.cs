namespace MlxPep.Core.Profiling;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

/// <summary>
/// Maps recommendation manifests to Profile DTOs with tier normalization.
/// Generates stable IDs using SHA256 and preserves sampler/hardware settings.
/// </summary>
public class RecommendationMapper
{
    public List<Profile> MapToProfiles(RecommendationManifest manifest)
    {
        if (manifest == null)
            throw new ArgumentNullException(nameof(manifest));

        Debug.WriteLine($"[RecommendationMapper] Mapping {manifest.Recommendations.Count} recommendations to profiles");

        var profiles = new List<Profile>();

        var tierOrder = new[] { "high", "balanced", "efficient" };

        foreach (var tier in tierOrder)
        {
            var tierKey = manifest.Recommendations.Keys.FirstOrDefault(
                k => k.Equals(tier, StringComparison.OrdinalIgnoreCase));

            if (tierKey == null)
            {
                Debug.WriteLine($"[RecommendationMapper] Tier '{tier}' not found in manifest");
                continue;
            }

            var tierRec = manifest.Recommendations[tierKey];
            var profile = MapTierToProfile(manifest, tierRec, tier);
            profiles.Add(profile);

            Debug.WriteLine($"[RecommendationMapper] Created profile for tier '{tier}' with ID {profile.Id}");
        }

        if (profiles.Count != 3)
            Debug.WriteLine($"[RecommendationMapper] Warning: Expected 3 tiers, got {profiles.Count}");

        return profiles;
    }

    private Profile MapTierToProfile(
        RecommendationManifest manifest,
        TierRecommendation tierRec,
        string tier)
    {
        var id = GenerateStableId(manifest.ModelHfId, tier);

        // Build system settings
        var system = new Dictionary<string, object>();
        if (tierRec.System != null)
        {
            foreach (var kv in tierRec.System)
                system[kv.Key] = kv.Value ?? string.Empty;
        }

        // Build omlx settings
        var omlx = new Dictionary<string, object>();
        if (tierRec.Omlx != null)
        {
            foreach (var kv in tierRec.Omlx)
                omlx[kv.Key] = kv.Value ?? string.Empty;
        }

        // Build harness settings
        var harness = new Dictionary<string, object>();
        if (tierRec.Harness != null)
        {
            foreach (var kv in tierRec.Harness)
                harness[kv.Key] = kv.Value ?? new Dictionary<string, object>();
        }

        // Extract sampler settings if present
        SamplerSettings? sampler = null;
        if (tierRec.Sampler != null)
        {
            double? temperature = null;
            double? topP = null;
            int? topK = null;
            double? repPenalty = null;
            int? contextTokens = null;

            if (tierRec.Sampler.ContainsKey("temperature") && tierRec.Sampler["temperature"] is double tempVal)
                temperature = tempVal;
            if (tierRec.Sampler.ContainsKey("topP") && tierRec.Sampler["topP"] is double topPVal)
                topP = topPVal;
            if (tierRec.Sampler.ContainsKey("topK") && tierRec.Sampler["topK"] is int topKVal)
                topK = topKVal;
            if (tierRec.Sampler.ContainsKey("repetitionPenalty") && tierRec.Sampler["repetitionPenalty"] is double repVal)
                repPenalty = repVal;
            if (tierRec.Sampler.ContainsKey("contextTokens") && tierRec.Sampler["contextTokens"] is int contextTokenValue && contextTokenValue > 0)
                contextTokens = contextTokenValue;  // Only set if > 0 (skip 0 as invalid)

            sampler = new SamplerSettings(temperature, topP, topK, repPenalty, contextTokens);
        }

        // Build hardware fingerprint
        var hardware = new HardwareFingerprint(
            Chip: manifest.Hardware?.Chip ?? "Unknown",
            MemoryGb: manifest.Hardware?.MemoryGb ?? 0,
            ModelIdentifier: manifest.Hardware?.ModelIdentifier ?? "Unknown");

        var profile = new Profile(
            SchemaVersion: 1,
            Id: id,
            ModelHfId: manifest.ModelHfId,
            Tier: tier,
            Engine: "mlx",
            System: system,
            OMLXSettings: omlx,
            Harness: harness,
            Provenance: new ProfileProvenance(
                Author: "model-assessor",
                CreatedAt: DateTime.UtcNow.ToString("O"),
                Source: "assess-command:workload-winner-collapse"),
            Hardware: hardware,
            Sampler: sampler);

        return profile;
    }

    private string GenerateStableId(string modelHfId, string tier)
    {
        // SHA256 of modelHfId + tier ensures stable, unique IDs
        var input = $"{modelHfId}:{tier}";
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        // Return first 12 chars for readability
        return $"{modelHfId.Split('/').Last()}-{tier}-{hex.Substring(0, 12)}";
    }
}
