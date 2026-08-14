namespace MlxPep.Core.Tests;

using System.Collections.Generic;
using System.Text.Json;
using MlxPep.Core;

public class ProfileSerializationTests
{
    [Fact]
    public void ProfileSerializer_SerializesObjectBackedBooleanValues()
    {
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "nemotron-balanced-test",
            ModelHfId: "mlx-community/NVIDIA-Nemotron-3.5-Lightning-30B-A3B-4bit",
            Tier: "balanced",
            Engine: "omlx",
            System: new Dictionary<string, object>
            {
                ["assessment_workload"] = "short_code_research_tools"
            },
            OMLXSettings: new Dictionary<string, object>
            {
                ["mtp_enabled"] = false,
                ["vlm_mtp_enabled"] = false,
                ["max_context_window"] = 16384
            },
            Harness: new Dictionary<string, object>
            {
                ["vscode"] = new Dictionary<string, object>
                {
                    ["maxInputTokens"] = 16384,
                    ["maxOutputTokens"] = 1536
                }
            },
            Provenance: new ProfileProvenance(
                Author: "test",
                CreatedAt: "2026-08-14T00:00:00Z",
                Source: "assess-command"),
            Hardware: new HardwareFingerprint(
                Chip: "Apple M4 Max",
                MemoryGb: 128,
                ModelIdentifier: "Mac16,5"),
            Sampler: new SamplerSettings(
                Temperature: 0.2,
                TopP: 0.95,
                TopK: 64,
                RepetitionPenalty: null,
                ContextTokens: 16384));

        var json = JsonSerializer.Serialize(profile, ProfileJsonSerializerContext.Default.Profile);

        Assert.Contains("\"mtp_enabled\":false", json);
        Assert.Contains("\"vlm_mtp_enabled\":false", json);
        Assert.Contains("\"max_context_window\":16384", json);
    }
}