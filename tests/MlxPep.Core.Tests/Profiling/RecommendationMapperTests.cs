namespace MlxPep.Core.Tests.Profiling;

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using MlxPep.Core.Profiling;

public class RecommendationMapperTests
{
    private readonly RecommendationMapper _mapper = new();

    [Fact]
    public void MapToProfiles_WithValidManifest_Returns3Profiles()
    {
        // Arrange
        var manifest = CreateTestManifest();

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert
        Assert.Equal(3, profiles.Count);
        Assert.Single(profiles, p => p.Tier == "high");
        Assert.Single(profiles, p => p.Tier == "balanced");
        Assert.Single(profiles, p => p.Tier == "efficient");
    }

    [Fact]
    public void MapToProfiles_ContainsCorrectModelHfId()
    {
        // Arrange
        var manifest = CreateTestManifest();

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert
        Assert.True(profiles.All(p => p.ModelHfId == "meta-llama/Llama-2-7b"));
    }

    [Fact]
    public void MapToProfiles_GeneratesStableIds()
    {
        // Arrange
        var manifest = CreateTestManifest();

        // Act
        var profiles1 = _mapper.MapToProfiles(manifest);
        var profiles2 = _mapper.MapToProfiles(manifest);

        // Assert - same manifest should produce same IDs
        for (int i = 0; i < profiles1.Count; i++)
        {
            Assert.Equal(profiles1[i].Id, profiles2[i].Id);
        }
    }

    [Fact]
    public void MapToProfiles_IdContainsTierName()
    {
        // Arrange
        var manifest = CreateTestManifest();

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert
        var highProfile = profiles.First(p => p.Tier == "high");
        Assert.Contains("high", highProfile.Id);

        var balancedProfile = profiles.First(p => p.Tier == "balanced");
        Assert.Contains("balanced", balancedProfile.Id);

        var efficientProfile = profiles.First(p => p.Tier == "efficient");
        Assert.Contains("efficient", efficientProfile.Id);
    }

    [Fact]
    public void MapToProfiles_PreservesSystemSettings()
    {
        // Arrange
        var manifest = CreateTestManifest();

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert
        var highProfile = profiles.First(p => p.Tier == "high");
        Assert.True(highProfile.System.ContainsKey("os"));
        Assert.Equal("macOS", highProfile.System["os"]);
    }

    [Fact]
    public void MapToProfiles_PreservesOmlxSettings()
    {
        // Arrange
        var manifest = CreateTestManifest();

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert
        var highProfile = profiles.First(p => p.Tier == "high");
        Assert.True(highProfile.OMLXSettings.ContainsKey("compute_units"));
        Assert.Equal("ALL", highProfile.OMLXSettings["compute_units"]);
    }

    [Fact]
    public void MapToProfiles_PreservesHardwareInfo()
    {
        // Arrange
        var manifest = CreateTestManifest();

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert
        var highProfile = profiles.First(p => p.Tier == "high");
        Assert.Equal("Apple M1", highProfile.Hardware.Chip);
        Assert.Equal(16, highProfile.Hardware.MemoryGb);
    }

    [Fact]
    public void MapToProfiles_SamplerSettingsExtracted()
    {
        // Arrange
        var manifest = CreateTestManifest();

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert
        var highProfile = profiles.First(p => p.Tier == "high");
        Assert.NotNull(highProfile.Sampler);
        Assert.Equal(0.7, highProfile.Sampler!.Temperature);
        Assert.Equal(16384, highProfile.Sampler.ContextTokens);
    }

    [Fact]
    public void MapToProfiles_AllProfilesHaveEngine()
    {
        // Arrange
        var manifest = CreateTestManifest();

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert
        Assert.True(profiles.All(p => p.Engine == "mlx"));
    }

    [Fact]
    public void MapToProfiles_AllProfilesHaveProvenance()
    {
        // Arrange
        var manifest = CreateTestManifest();

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert
        Assert.True(profiles.All(p => p.Provenance != null));
        Assert.True(profiles.All(p => p.Provenance.Author == "model-assessor"));
        Assert.True(profiles.All(p => p.Provenance.Source == "assess-command:workload-winner-collapse"));
    }

    [Fact]
    public void MapToProfiles_WithNullManifest_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _mapper.MapToProfiles(null!));
    }

    [Fact]
    public void MapToProfiles_TierOrderingIsCorrect()
    {
        // Arrange
        var manifest = CreateTestManifest();

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert - high, balanced, efficient in that order
        Assert.Equal("high", profiles[0].Tier);
        Assert.Equal("balanced", profiles[1].Tier);
        Assert.Equal("efficient", profiles[2].Tier);
    }

    [Fact]
    public void MapToProfiles_WithoutSamplerInManifest_CreatesSamplerAsNull()
    {
        // Arrange
        var manifest = new RecommendationManifest(
            ModelHfId: "test/model",
            AssessmentVersion: "1.0.0",
            Timestamp: DateTime.UtcNow.ToString("O"),
            Recommendations: new Dictionary<string, TierRecommendation>
            {
                ["high"] = new TierRecommendation(
                    Tier: "high",
                    System: new Dictionary<string, object> { { "os", "Linux" } },
                    Omlx: new Dictionary<string, object> { { "compute_units", "ALL" } },
                    Harness: new Dictionary<string, object> { { "framework", "vscode" } },
                    Sampler: null),
                ["balanced"] = new TierRecommendation(
                    Tier: "balanced",
                    System: new Dictionary<string, object> { { "os", "Linux" } },
                    Omlx: new Dictionary<string, object> { { "compute_units", "GPU" } },
                    Harness: new Dictionary<string, object> { { "framework", "vscode" } },
                    Sampler: null),
                ["efficient"] = new TierRecommendation(
                    Tier: "efficient",
                    System: new Dictionary<string, object> { { "os", "Linux" } },
                    Omlx: new Dictionary<string, object> { { "compute_units", "CPU" } },
                    Harness: new Dictionary<string, object> { { "framework", "vscode" } },
                    Sampler: null)
            });

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert
        Assert.True(profiles.All(p => p.Sampler == null));
    }

    private RecommendationManifest CreateTestManifest()
    {
        return new RecommendationManifest(
            ModelHfId: "meta-llama/Llama-2-7b",
            AssessmentVersion: "1.0.0",
            Timestamp: DateTime.UtcNow.ToString("O"),
            Recommendations: new Dictionary<string, TierRecommendation>
            {
                ["high"] = new TierRecommendation(
                    Tier: "high",
                    System: new Dictionary<string, object> { { "os", "macOS" } },
                    Omlx: new Dictionary<string, object> { { "compute_units", "ALL" } },
                    Harness: new Dictionary<string, object> { { "framework", "vscode" } },
                    Sampler: new Dictionary<string, object> { { "temperature", 0.7 }, { "contextTokens", 16384 } }),
                ["balanced"] = new TierRecommendation(
                    Tier: "balanced",
                    System: new Dictionary<string, object> { { "os", "macOS" } },
                    Omlx: new Dictionary<string, object> { { "compute_units", "GPU" } },
                    Harness: new Dictionary<string, object> { { "framework", "vscode" } },
                    Sampler: new Dictionary<string, object> { { "temperature", 0.7 }, { "contextTokens", 8192 } }),
                ["efficient"] = new TierRecommendation(
                    Tier: "efficient",
                    System: new Dictionary<string, object> { { "os", "macOS" } },
                    Omlx: new Dictionary<string, object> { { "compute_units", "CPU" } },
                    Harness: new Dictionary<string, object> { { "framework", "vscode" } },
                    Sampler: new Dictionary<string, object> { { "temperature", 0.7 }, { "contextTokens", 4096 } })
            },
            Hardware: new HardwareAssessment("Apple M1", 16, "MacBook"));
    }
}
