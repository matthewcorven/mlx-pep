namespace MlxPep.Core.Profiling;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// Maps a recommendation manifest from model-assessor to mlx-pep Profile records.
/// Converts tier recommendations to three Profile objects (high/balanced/efficient).
/// </summary>
public class RecommendationMapper
{
    /// <summary>
    /// Maps a recommendation manifest to three Profile records (one per tier).
    /// </summary>
    public List<Profile> MapToProfiles(
        RecommendationManifest manifest,
        string author = "assess-command",
        string? hardwareChip = null,
        int? hardwareMemoryGb = null,
        string? hardwareModelId = null)
    {
        try
        {
            Debug.WriteLine($"[RecommendationMapper] Mapping manifest for {manifest.ModelHfId}");
            var profiles = new List<Profile>();

            foreach (var tierKey in new[] { "high", "balanced", "efficient" })
            {
                if (!manifest.Recommendations.TryGetValue(tierKey, out var tierRec))
                {
                    Debug.WriteLine($"[RecommendationMapper] Recommendation not found for tier: {tierKey}");
                    throw new InvalidOperationException($"Recommendation manifest missing tier: {tierKey}");
                }

                Debug.WriteLine($"[RecommendationMapper] Processing tier: {tierKey}");
                var profile = MapTierToProfile(
                    manifest,
                    tierRec,
                    author,
                    hardwareChip,
                    hardwareMemoryGb,
                    hardwareModelId);

                profiles.Add(profile);
            }

            Debug.WriteLine($"[RecommendationMapper] Successfully mapped {profiles.Count} profiles");
            return profiles;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RecommendationMapper] Error mapping manifest: {ex.Message}");
            throw;
        }
    }

    private Profile MapTierToProfile(
        RecommendationManifest manifest,
        TierRecommendation tierRec,
        string author,
        string? hardwareChip,
        int? hardwareMemoryGb,
        string? hardwareModelId)
    {
        Debug.WriteLine($"[RecommendationMapper] Creating Profile for tier {tierRec.Tier}");

        // Generate stable profile ID
        var profileId = GenerateProfileId(manifest.ModelHfId, tierRec.Tier, tierRec);
        Debug.WriteLine($"[RecommendationMapper] Generated profile ID: {profileId}");

        // Map sampler settings, converting Dictionary to strongly-typed record if needed
        SamplerSettings? sampler = null;
        if (tierRec.Sampler != null && tierRec.Sampler.Count > 0)
        {
            Debug.WriteLine($"[RecommendationMapper] Mapping sampler settings");
            sampler = MapSamplerSettings(tierRec.Sampler);
        }

        // Use provided hardware or extract from manifest
        var hardware = new HardwareFingerprint(
            chip: hardwareChip ?? manifest.Hardware?.Chip ?? "Unknown",
            memoryGb: hardwareMemoryGb ?? manifest.Hardware?.MemoryGb ?? 16,
            modelIdentifier: hardwareModelId ?? manifest.Hardware?.ModelIdentifier ?? "Unknown");

        Debug.WriteLine($"[RecommendationMapper] Hardware fingerprint: {hardware.Chip}, {hardware.MemoryGb}GB");

        var profile = new Profile(
            SchemaVersion: 1,
            Id: profileId,
            ModelHfId: manifest.ModelHfId,
            Tier: NormalizeTier(tierRec.Tier),
            Engine: "omlx",
            System: tierRec.System,
            OMLXSettings: tierRec.Omlx,
            Harness: tierRec.Harness,
            Provenance: new ProfileProvenance(
                Author: author,
                CreatedAt: manifest.Timestamp,
                Source: "assess"),
            Hardware: hardware,
            Sampler: sampler);

        Debug.WriteLine($"[RecommendationMapper] Created profile: {profile.Id}");
        return profile;
    }

    private SamplerSettings MapSamplerSettings(Dictionary<string, object> samplerDict)
    {
        try
        {
            Debug.WriteLine($"[RecommendationMapper] Converting sampler dictionary to SamplerSettings");

            double? temperature = ExtractDouble(samplerDict, "temperature");
            double? topP = ExtractDouble(samplerDict, "topP", "top_p");
            int? topK = ExtractInt(samplerDict, "topK", "top_k");
            double? repetitionPenalty = ExtractDouble(samplerDict, "repetitionPenalty", "repetition_penalty");
            int? contextTokens = ExtractInt(samplerDict, "contextTokens", "context_tokens");

            return new SamplerSettings(
                Temperature: temperature,
                TopP: topP,
                TopK: topK,
                RepetitionPenalty: repetitionPenalty,
                ContextTokens: contextTokens);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RecommendationMapper] Error mapping sampler settings: {ex.Message}");
            throw;
        }
    }

    private double? ExtractDouble(Dictionary<string, object> dict, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (dict.TryGetValue(key, out var value))
            {
                if (value is double d)
                    return d;
                if (value is JsonElement elem && elem.TryGetDouble(out var elemDouble))
                    return elemDouble;
                if (double.TryParse(value?.ToString(), out var parsed))
                    return parsed;
            }
        }
        return null;
    }

    private int? ExtractInt(Dictionary<string, object> dict, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (dict.TryGetValue(key, out var value))
            {
                if (value is int i)
                    return i;
                if (value is long l)
                    return (int)l;
                if (value is JsonElement elem && elem.TryGetInt32(out var elemInt))
                    return elemInt;
                if (int.TryParse(value?.ToString(), out var parsed))
                    return parsed;
            }
        }
        return null;
    }

    private string NormalizeTier(string tier)
    {
        // Normalize common tier names to mlx-pep standard
        return tier.ToLowerInvariant() switch
        {
            "high" or "high-performance" or "high-perf" => "high",
            "balanced" or "balanced-performance" => "balanced",
            "efficient" or "efficiency" or "low-power" => "efficient",
            _ => tier.ToLowerInvariant()
        };
    }

    private string GenerateProfileId(string modelHfId, string tier, TierRecommendation tierRec)
    {
        // Create a stable hash from the settings to detect changes
        var settingsHash = GenerateSettingsHash(tierRec);
        var modelSlug = modelHfId.Replace("/", "-").ToLowerInvariant();
        var tierSlug = NormalizeTier(tier);

        return $"{modelSlug}-{tierSlug}-{settingsHash}";
    }

    private string GenerateSettingsHash(TierRecommendation tierRec)
    {
        try
        {
            Debug.WriteLine($"[RecommendationMapper] Generating settings hash");

            // Create a JSON representation of key settings
            var settingsStr = JsonSerializer.Serialize(new
            {
                tierRec.System,
                tierRec.Omlx,
                tierRec.Harness,
                tierRec.Sampler
            });

            // Generate SHA256 hash and take first 8 chars
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(settingsStr));
                var hashStr = Convert.ToHexString(hash).ToLowerInvariant().Substring(0, 8);
                Debug.WriteLine($"[RecommendationMapper] Generated hash: {hashStr}");
                return hashStr;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RecommendationMapper] Error generating hash: {ex.Message}");
            // Fallback to timestamp-based hash if computation fails
            return DateTime.UtcNow.Ticks.ToString("x");
        }
    }
}
