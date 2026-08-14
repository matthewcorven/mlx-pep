namespace MlxPep.Core.Tests.Fixtures;

using System;
using System.Collections.Generic;

/// <summary>
/// Test fixtures for Profile records.
/// Issue #17: Provides templates for valid/invalid profiles used in profiling tests.
/// </summary>
public static class ProfileFixtures
{
    /// <summary>
    /// Create a valid profile template for a given model, tier, and source.
    /// </summary>
    public static Profile CreateValidProfile(
        string modelHfId = "meta-llama/Llama-2-7b",
        string tier = "balanced",
        string source = "assess",
        string? author = null)
    {
        author ??= Environment.UserName;
        var now = DateTime.UtcNow.ToString("O");
        var tierId = $"{modelHfId.Split('/').Last()}-{tier[0]}{now.GetHashCode():x}".Replace("-", "").Substring(0, 12);

        return new Profile(
            SchemaVersion: 1,
            Id: $"{tierId}-{tier}",
            ModelHfId: modelHfId,
            Tier: tier,
            Engine: "omlx",
            System: new Dictionary<string, object>
            {
                { "iogpu.wired_limit_mb", 4096 }
            },
            OMLXSettings: new Dictionary<string, object>
            {
                { "memory_guard_tier", tier },
                { "memory_guard_ceiling_gb", 12 }
            },
            Harness: new Dictionary<string, object>
            {
                { "vscode", new Dictionary<string, object> { { "maxInputTokens", 32000 } } },
                { "copilotCli", new Dictionary<string, object> { { "maxPromptTokens", 32000 } } }
            },
            Provenance: new ProfileProvenance(
                Author: author,
                CreatedAt: now,
                Source: source
            ),
            Hardware: new HardwareFingerprint(
                Chip: "Apple M3 Pro",
                MemoryGb: 18,
                ModelIdentifier: "MacBookPro18,1"
            ),
            Sampler: new SamplerSettings(
                Temperature: 0.7,
                TopP: 0.95,
                TopK: 20,
                RepetitionPenalty: 1.02,
                ContextTokens: 2048
            )
        );
    }

    /// <summary>
    /// Create a profile with custom tier and memory settings (for tier variation tests).
    /// </summary>
    public static Profile CreateProfileForTier(string tier)
    {
        return tier switch
        {
            "high" => new Profile(
                SchemaVersion: 1,
                Id: "llama7b-high-test",
                ModelHfId: "meta-llama/Llama-2-7b",
                Tier: "high",
                Engine: "omlx",
                System: new Dictionary<string, object> { { "iogpu.wired_limit_mb", 6144 } },
                OMLXSettings: new Dictionary<string, object> { { "memory_guard_tier", "high" }, { "memory_guard_ceiling_gb", 16 } },
                Harness: new Dictionary<string, object>
                {
                    { "vscode", new Dictionary<string, object> { { "maxInputTokens", 64000 } } }
                },
                Provenance: new ProfileProvenance("test", DateTime.UtcNow.ToString("O"), "assess"),
                Hardware: new HardwareFingerprint("Apple M3 Pro", 18, "MacBookPro18,1"),
                Sampler: new SamplerSettings(Temperature: 0.7, TopP: 0.95, TopK: 20)
            ),
            "balanced" => new Profile(
                SchemaVersion: 1,
                Id: "llama7b-balanced-test",
                ModelHfId: "meta-llama/Llama-2-7b",
                Tier: "balanced",
                Engine: "omlx",
                System: new Dictionary<string, object> { { "iogpu.wired_limit_mb", 4096 } },
                OMLXSettings: new Dictionary<string, object> { { "memory_guard_tier", "balanced" }, { "memory_guard_ceiling_gb", 12 } },
                Harness: new Dictionary<string, object>
                {
                    { "vscode", new Dictionary<string, object> { { "maxInputTokens", 32000 } } }
                },
                Provenance: new ProfileProvenance("test", DateTime.UtcNow.ToString("O"), "assess"),
                Hardware: new HardwareFingerprint("Apple M3 Pro", 18, "MacBookPro18,1"),
                Sampler: new SamplerSettings(Temperature: 0.7, TopP: 0.95, TopK: 20)
            ),
            "efficient" => new Profile(
                SchemaVersion: 1,
                Id: "llama7b-efficient-test",
                ModelHfId: "meta-llama/Llama-2-7b",
                Tier: "efficient",
                Engine: "omlx",
                System: new Dictionary<string, object> { { "iogpu.wired_limit_mb", 2048 } },
                OMLXSettings: new Dictionary<string, object> { { "memory_guard_tier", "efficient" }, { "memory_guard_ceiling_gb", 8 } },
                Harness: new Dictionary<string, object>
                {
                    { "vscode", new Dictionary<string, object> { { "maxInputTokens", 16000 } } }
                },
                Provenance: new ProfileProvenance("test", DateTime.UtcNow.ToString("O"), "assess"),
                Hardware: new HardwareFingerprint("Apple M3 Pro", 18, "MacBookPro18,1"),
                Sampler: new SamplerSettings(Temperature: 0.7, TopP: 0.95, TopK: 20)
            ),
            _ => throw new ArgumentException($"Unknown tier: {tier}")
        };
    }

    /// <summary>
    /// Create a profile set (three tiers) for a given model.
    /// </summary>
    public static Profile[] CreateProfileSet(
        string modelHfId = "meta-llama/Llama-2-7b",
        string source = "assess")
    {
        return new[]
        {
            CreateValidProfile(modelHfId, "high", source),
            CreateValidProfile(modelHfId, "balanced", source),
            CreateValidProfile(modelHfId, "efficient", source)
        };
    }

    /// <summary>
    /// Create a profile with invalid schema version (for negative tests).
    /// </summary>
    public static Profile CreateInvalidSchemaVersion()
    {
        return CreateValidProfile() with { SchemaVersion = 999 };
    }

    /// <summary>
    /// Create a profile with invalid tier (for validation tests).
    /// </summary>
    public static Profile CreateInvalidTier()
    {
        return CreateValidProfile() with { Tier = "ultra-high" };
    }

    /// <summary>
    /// Create a profile missing hardware fingerprint data.
    /// </summary>
    public static Profile CreateInvalidHardware()
    {
        return CreateValidProfile() with
        {
            Hardware = new HardwareFingerprint("", 0, "")
        };
    }

    /// <summary>
    /// JSONL string: three-tier profile set ready for writing to file.
    /// </summary>
    public static string GetProfileSetJsonL(string modelHfId = "meta-llama/Llama-2-7b")
    {
        var profiles = CreateProfileSet(modelHfId);
        var lines = profiles.Select(p =>
            System.Text.Json.JsonSerializer.Serialize(
                p,
                ProfileJsonSerializerContext.Default.Profile
            )
        );
        return string.Join(Environment.NewLine, lines);
    }
}
