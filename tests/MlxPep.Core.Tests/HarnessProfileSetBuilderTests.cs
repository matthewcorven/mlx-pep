namespace MlxPep.Core.Tests;

using System.Collections.Generic;
using Xunit;

public class HarnessProfileSetBuilderTests
{
    [Fact]
    public void BuildApplyProfile_ForVscode_AggregatesAllTierModelsAndDefaults()
    {
        var profiles = CreateProfiles();

        var aggregate = HarnessProfileSetBuilder.BuildApplyProfile(profiles, "vscode");

        var vscode = Assert.IsType<Dictionary<string, object>>(aggregate.Harness["vscode"]);
        var customSettings = Assert.IsType<Dictionary<string, object>>(vscode["customSettings"]);
        var chatLanguageModels = Assert.IsType<Dictionary<string, object>>(vscode["chatLanguageModels"]);
        var models = Assert.IsType<Dictionary<string, object>>(chatLanguageModels["models"]);

        Assert.Equal("test-balanced", customSettings["inlineChat.defaultModel"]);
        Assert.Equal("test-balanced", customSettings["chat.utilityModel"]);
        Assert.Equal("test-efficient", customSettings["chat.utilitySmallModel"]);
        Assert.Equal(3, models.Count);
        Assert.Contains("test-high", models.Keys);
        Assert.Contains("test-balanced", models.Keys);
        Assert.Contains("test-efficient", models.Keys);
    }

    [Fact]
    public void BuildApplyProfile_ForCopilotCli_AggregatesAllTierProfiles()
    {
        var profiles = CreateProfiles();

        var aggregate = HarnessProfileSetBuilder.BuildApplyProfile(profiles, "copilot-cli");

        var copilot = Assert.IsType<Dictionary<string, object>>(aggregate.Harness["copilotCli"]);
        var aggregatedProfiles = Assert.IsType<Dictionary<string, object>>(copilot["profiles"]);

        Assert.Equal("test-balanced", copilot["defaultProfile"]);
        Assert.Equal(3, aggregatedProfiles.Count);
        Assert.Contains("test-high", aggregatedProfiles.Keys);
        Assert.Contains("test-balanced", aggregatedProfiles.Keys);
        Assert.Contains("test-efficient", aggregatedProfiles.Keys);
    }

    [Fact]
    public void BuildApplyProfile_ForAggregatedHarness_ThrowsWhenTierMissing()
    {
        var profiles = new List<Profile>
        {
            CreateProfile("test-balanced", "balanced", 16384, 1536, 0.2, 0.95, 64),
            CreateProfile("test-efficient", "efficient", 8192, 1024, 0.1, 0.9, 40)
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            HarnessProfileSetBuilder.BuildApplyProfile(profiles, "copilot-cli"));

        Assert.Contains("missing required tier 'high'", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static List<Profile> CreateProfiles()
    {
        return new List<Profile>
        {
            CreateProfile("test-high", "high", 32768, 4096, 0.1, 0.9, 40),
            CreateProfile("test-balanced", "balanced", 16384, 1536, 0.2, 0.95, 64),
            CreateProfile("test-efficient", "efficient", 8192, 1024, 0.1, 0.9, 40)
        };
    }

    private static Profile CreateProfile(string id, string tier, int maxInputTokens, int maxOutputTokens, double temperature, double topP, int topK)
    {
        return new Profile(
            SchemaVersion: 1,
            Id: id,
            ModelHfId: "mlx-community/NVIDIA-Nemotron-3.5-Lightning-30B-A3B-4bit",
            Tier: tier,
            Engine: "omlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>
            {
                ["vscode"] = new Dictionary<string, object>
                {
                    ["maxInputTokens"] = maxInputTokens,
                    ["maxOutputTokens"] = maxOutputTokens,
                    ["chatLanguageModels"] = new Dictionary<string, object>
                    {
                        ["models"] = new Dictionary<string, object>
                        {
                            [id] = new Dictionary<string, object>
                            {
                                ["name"] = id,
                                ["maxInputTokens"] = maxInputTokens,
                                ["maxOutputTokens"] = maxOutputTokens
                            }
                        }
                    }
                },
                ["copilotCli"] = new Dictionary<string, object>
                {
                    ["maxPromptTokens"] = maxInputTokens,
                    ["contextWindow"] = maxInputTokens,
                    ["modelId"] = "NVIDIA-Nemotron-3.5-Lightning-30B-A3B-4bit",
                    ["temperature"] = temperature,
                    ["topP"] = topP,
                    ["topK"] = topK
                },
                ["opencode"] = new Dictionary<string, object>
                {
                    ["maxInputTokens"] = maxInputTokens,
                    ["maxOutputTokens"] = maxOutputTokens,
                    ["modelId"] = "NVIDIA-Nemotron-3.5-Lightning-30B-A3B-4bit"
                },
                ["claude-code"] = new Dictionary<string, object>
                {
                    ["maxInputTokens"] = maxInputTokens,
                    ["maxOutputTokens"] = maxOutputTokens,
                    ["modelId"] = "NVIDIA-Nemotron-3.5-Lightning-30B-A3B-4bit"
                }
            },
            Provenance: new ProfileProvenance("test", "2026-08-14T00:00:00Z", "assess"),
            Hardware: new HardwareFingerprint("Apple M4 Max", 128, "Mac16,5"),
            Sampler: new SamplerSettings(temperature, topP, topK, null, maxInputTokens));
    }
}