namespace MlxPep.Core.Tests;

using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using MlxPep.Core.Tests.Fixtures;

/// <summary>
/// Tests for profiling pipeline core logic.
/// Issue #17: Test scaffolding for UC4 profiling via model-assessor.
/// Validates profile generation from assessor manifests, tier emission, and schema compliance.
/// </summary>
public class ProfilingTests
{
    /// <summary>
    /// Helper to parse assessor manifest JSON and extract tier recommendations.
    /// </summary>
    private static JsonElement ParseAssessorManifest(string json)
    {
        return JsonDocument.Parse(json).RootElement;
    }

    /// <summary>
    /// Helper to extract a single tier from assessor manifest.
    /// </summary>
    private static JsonElement GetTierFromManifest(JsonElement manifest, string tier)
    {
        return manifest
            .GetProperty("tier_recommendations")
            .GetProperty(tier);
    }

    [Fact]
    public void Profiling_ParseAssessorManifest_SucceedsWithValidJson()
    {
        // Arrange
        var json = AssessorFixtures.Llama7bRecommendationManifest;

        // Act
        var manifest = ParseAssessorManifest(json);

        // Assert
        Assert.True(manifest.TryGetProperty("model_id", out var modelId));
        Assert.Equal("meta-llama/Llama-2-7b", modelId.GetString());
    }

    [Fact]
    public void Profiling_AssessorManifest_ContainsAllThreeTiers()
    {
        // Arrange
        var json = AssessorFixtures.Llama7bRecommendationManifest;
        var manifest = ParseAssessorManifest(json);
        var tiers = manifest.GetProperty("tier_recommendations");

        // Act & Assert: verify all three tiers present
        Assert.True(tiers.TryGetProperty("high", out _), "Manifest must contain 'high' tier");
        Assert.True(tiers.TryGetProperty("balanced", out _), "Manifest must contain 'balanced' tier");
        Assert.True(tiers.TryGetProperty("efficient", out _), "Manifest must contain 'efficient' tier");
    }

    [Fact]
    public void Profiling_TierRecommendations_ContainRequiredFields()
    {
        // Arrange
        var json = AssessorFixtures.Llama7bRecommendationManifest;
        var manifest = ParseAssessorManifest(json);
        var highTier = GetTierFromManifest(manifest, "high");

        // Act & Assert: verify required fields in tier
        Assert.True(highTier.TryGetProperty("reason", out _), "Tier must have 'reason'");
        Assert.True(highTier.TryGetProperty("system", out _), "Tier must have 'system'");
        Assert.True(highTier.TryGetProperty("omlx", out _), "Tier must have 'omlx'");
        Assert.True(highTier.TryGetProperty("harness", out _), "Tier must have 'harness'");
    }

    [Fact]
    public void Profiling_CreateValidProfile_SetsCorrectDefaults()
    {
        // Arrange & Act
        var profile = ProfileFixtures.CreateValidProfile(
            modelHfId: "test-model/test-id",
            tier: "high",
            source: "assess"
        );

        // Assert
        Assert.Equal(1, profile.SchemaVersion);
        Assert.Equal("test-model/test-id", profile.ModelHfId);
        Assert.Equal("high", profile.Tier);
        Assert.Equal("omlx", profile.Engine);
        Assert.Equal("assess", profile.Provenance.Source);
        Assert.NotNull(profile.Hardware);
    }

    [Fact]
    public void Profiling_ProfileTiers_MatchSpecification()
    {
        // Arrange
        var validTiers = new[] { "high", "balanced", "efficient" };

        // Act & Assert: each tier should produce a valid profile
        foreach (var tier in validTiers)
        {
            var profile = ProfileFixtures.CreateProfileForTier(tier);
            Assert.Equal(tier, profile.Tier);
            Assert.NotNull(profile.OMLXSettings);
            Assert.NotNull(profile.System);
        }
    }

    [Fact]
    public void Profiling_ProfileSet_EmitsExactlyThreeTiers()
    {
        // Arrange & Act
        var profiles = ProfileFixtures.CreateProfileSet("test-model/test-id");

        // Assert
        Assert.Equal(3, profiles.Length);
        Assert.Single(profiles, p => p.Tier == "high");
        Assert.Single(profiles, p => p.Tier == "balanced");
        Assert.Single(profiles, p => p.Tier == "efficient");
    }

    [Fact]
    public void Profiling_ProfileSet_AllHaveSameModel()
    {
        // Arrange
        var modelId = "shared-model/for-testing";

        // Act
        var profiles = ProfileFixtures.CreateProfileSet(modelId);

        // Assert
        foreach (var profile in profiles)
        {
            Assert.Equal(modelId, profile.ModelHfId);
        }
    }

    [Fact]
    public void Profiling_ProfileProvenance_SetsAssessSource()
    {
        // Arrange & Act
        var profile = ProfileFixtures.CreateValidProfile(source: "assess");

        // Assert
        Assert.Equal("assess", profile.Provenance.Source);
        Assert.NotNull(profile.Provenance.Author);
        Assert.NotNull(profile.Provenance.CreatedAt);
    }

    [Fact]
    public void Profiling_HardwareFingerprint_CapturesDeviceInfo()
    {
        // Arrange & Act
        var profile = ProfileFixtures.CreateValidProfile();

        // Assert
        Assert.NotEmpty(profile.Hardware.Chip);
        Assert.True(profile.Hardware.MemoryGb > 0, "Memory must be positive");
        Assert.NotEmpty(profile.Hardware.ModelIdentifier);
    }

    [Fact]
    public void Profiling_SamplerSettings_PropagateToAllTiers()
    {
        // Arrange & Act
        var profiles = ProfileFixtures.CreateProfileSet();

        // Assert: all tiers should have sampler config
        foreach (var profile in profiles)
        {
            Assert.NotNull(profile.Sampler);
            if (profile.Sampler != null)
            {
                Assert.NotNull(profile.Sampler.Temperature);
                Assert.NotNull(profile.Sampler.TopP);
            }
        }
    }

    [Fact]
    public void Profiling_HighTier_HasMoreResourcesThanEfficient()
    {
        // Arrange
        var high = ProfileFixtures.CreateProfileForTier("high");
        var efficient = ProfileFixtures.CreateProfileForTier("efficient");

        // Act & Assert: high tier should allocate more memory (via ceiling)
        var highCeiling = high.OMLXSettings["memory_guard_ceiling_gb"];
        var efficientCeiling = efficient.OMLXSettings["memory_guard_ceiling_gb"];

        Assert.True(
            Convert.ToDouble(highCeiling) > Convert.ToDouble(efficientCeiling),
            "High tier must allocate more memory than efficient tier"
        );
    }

    [Fact]
    public void Profiling_HarnesSettings_ArePresent()
    {
        // Arrange & Act
        var profile = ProfileFixtures.CreateValidProfile();

        // Assert
        Assert.NotNull(profile.Harness);
        Assert.True(profile.Harness.Count > 0, "Harness must have configuration entries");
        Assert.True(
            profile.Harness.ContainsKey("vscode") || profile.Harness.ContainsKey("copilotCli"),
            "Harness must configure at least one integration"
        );
    }

    [Fact]
    public void Profiling_ProfileJsonSerialization_RoundTrips()
    {
        // Arrange
        var profile = ProfileFixtures.CreateValidProfile();
        var json = System.Text.Json.JsonSerializer.Serialize(
            profile,
            ProfileJsonSerializerContext.Default.Profile
        );

        // Act
        var deserialized = System.Text.Json.JsonSerializer.Deserialize(
            json,
            ProfileJsonSerializerContext.Default.Profile
        );

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(profile.Id, deserialized!.Id);
        Assert.Equal(profile.ModelHfId, deserialized!.ModelHfId);
        Assert.Equal(profile.Tier, deserialized!.Tier);
    }

    [Fact]
    public void Profiling_SmokeSuiteManifest_IsValid()
    {
        // Arrange & Act
        var json = AssessorFixtures.GetFixture("smoke", "small");
        var manifest = ParseAssessorManifest(json);

        // Assert
        Assert.Equal("smoke", manifest.GetProperty("assessment_results").GetProperty("suite").GetString());
        var tiers = manifest.GetProperty("tier_recommendations");
        Assert.Equal(3, tiers.EnumerateObject().Count());
    }

    [Fact]
    public void Profiling_FullSuiteManifest_ExceedsSmokeSuiteDuration()
    {
        // Arrange
        var smokJson = AssessorFixtures.GetFixture("smoke", "small");
        var fullJson = AssessorFixtures.GetFixture("full", "small");

        var smokeManifest = ParseAssessorManifest(smokJson);
        var fullManifest = ParseAssessorManifest(fullJson);

        // Act
        var smokeDuration = smokeManifest.GetProperty("assessment_results").GetProperty("duration_seconds").GetInt32();
        var fullDuration = fullManifest.GetProperty("assessment_results").GetProperty("duration_seconds").GetInt32();

        // Assert
        Assert.True(fullDuration > smokeDuration, "Full suite should take longer than smoke suite");
    }

    [Fact]
    public void Profiling_LargModelManifest_HasHigherResourceRequirements()
    {
        // Arrange
        var smallJson = AssessorFixtures.GetFixture("full", "small");
        var largeJson = AssessorFixtures.GetFixture("full", "large");

        var smallManifest = ParseAssessorManifest(smallJson);
        var largeManifest = ParseAssessorManifest(largeJson);

        // Act
        var smallHardware = smallManifest.GetProperty("hardware_fingerprint");
        var largeHardware = largeManifest.GetProperty("hardware_fingerprint");

        var smallMemory = smallHardware.GetProperty("memory_gb").GetInt32();
        var largeMemory = largeHardware.GetProperty("memory_gb").GetInt32();

        // Assert
        Assert.True(largeMemory > smallMemory, "Large model fixture should target higher-memory hardware");
    }
}
