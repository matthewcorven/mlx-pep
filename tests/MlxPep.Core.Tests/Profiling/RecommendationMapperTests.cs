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

    [Fact]
    public void MapToProfiles_WithMissingHighTier_SkipsAndContinues()
    {
        // Arrange - only balanced and efficient, no high tier
        var manifest = new RecommendationManifest(
            ModelHfId: "test/model",
            AssessmentVersion: "1.0.0",
            Timestamp: DateTime.UtcNow.ToString("O"),
            Recommendations: new Dictionary<string, TierRecommendation>
            {
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

        // Assert - should have 2 profiles, not 3
        Assert.Equal(2, profiles.Count);
        Assert.DoesNotContain(profiles, p => p.Tier == "high");
        Assert.Single(profiles, p => p.Tier == "balanced");
        Assert.Single(profiles, p => p.Tier == "efficient");
    }

    [Fact]
    public void MapToProfiles_WithEmptyRecommendations_ReturnsEmptyList()
    {
        // Arrange - no tiers at all
        var manifest = new RecommendationManifest(
            ModelHfId: "test/model",
            AssessmentVersion: "1.0.0",
            Timestamp: DateTime.UtcNow.ToString("O"),
            Recommendations: new Dictionary<string, TierRecommendation>());

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert
        Assert.Empty(profiles);
    }

    [Fact]
    public void MapToProfiles_WithCaseMismatchedTierNames_HandlesCaseInsensitively()
    {
        // Arrange - use uppercase tier names to test case-insensitive lookup
        var manifest = new RecommendationManifest(
            ModelHfId: "test/model",
            AssessmentVersion: "1.0.0",
            Timestamp: DateTime.UtcNow.ToString("O"),
            Recommendations: new Dictionary<string, TierRecommendation>
            {
                ["HIGH"] = new TierRecommendation(
                    Tier: "high",
                    System: new Dictionary<string, object> { { "os", "Linux" } },
                    Omlx: new Dictionary<string, object> { { "compute_units", "ALL" } },
                    Harness: new Dictionary<string, object> { { "framework", "vscode" } },
                    Sampler: null),
                ["Balanced"] = new TierRecommendation(
                    Tier: "balanced",
                    System: new Dictionary<string, object> { { "os", "Linux" } },
                    Omlx: new Dictionary<string, object> { { "compute_units", "GPU" } },
                    Harness: new Dictionary<string, object> { { "framework", "vscode" } },
                    Sampler: null),
                ["EFFICIENT"] = new TierRecommendation(
                    Tier: "efficient",
                    System: new Dictionary<string, object> { { "os", "Linux" } },
                    Omlx: new Dictionary<string, object> { { "compute_units", "CPU" } },
                    Harness: new Dictionary<string, object> { { "framework", "vscode" } },
                    Sampler: null)
            });

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert - should handle case-insensitive lookup and find all 3 tiers
        Assert.Equal(3, profiles.Count);
        Assert.Single(profiles, p => p.Tier == "high");
        Assert.Single(profiles, p => p.Tier == "balanced");
        Assert.Single(profiles, p => p.Tier == "efficient");
    }

    // ===== ITERATION 2: Sampler Settings & Hardware Edge Cases =====

    [Fact]
    public void MapToProfiles_WithSamplerSettingsAsWrongType_SkipsInvalidTypes()
    {
        // Arrange - temperature stored as string instead of double
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
                    Sampler: new Dictionary<string, object>
                    {
                        { "temperature", "0.7" },  // Wrong type: string instead of double
                        { "topP", "0.9" },         // Wrong type: string instead of double
                        { "topK", "50" },          // Wrong type: string instead of int
                        { "contextTokens", 16384 } // Correct type
                    }),
                ["balanced"] = new TierRecommendation(
                    Tier: "balanced",
                    System: new Dictionary<string, object> { { "os", "Linux" } },
                    Omlx: new Dictionary<string, object> { { "compute_units", "GPU" } },
                    Harness: new Dictionary<string, object> { { "framework", "vscode" } },
                    Sampler: new Dictionary<string, object> { { "contextTokens", 8192 } }),
                ["efficient"] = new TierRecommendation(
                    Tier: "efficient",
                    System: new Dictionary<string, object> { { "os", "Linux" } },
                    Omlx: new Dictionary<string, object> { { "compute_units", "CPU" } },
                    Harness: new Dictionary<string, object> { { "framework", "vscode" } },
                    Sampler: new Dictionary<string, object> { { "contextTokens", 4096 } })
            },
            Hardware: new HardwareAssessment("Test CPU", 8, "TestMachine"));

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert - wrong-type properties are skipped, contextTokens is preserved
        var highProfile = profiles.First(p => p.Tier == "high");
        Assert.NotNull(highProfile.Sampler);
        Assert.Null(highProfile.Sampler!.Temperature);  // String was skipped
        Assert.Null(highProfile.Sampler.TopP);          // String was skipped
        Assert.Null(highProfile.Sampler.TopK);          // String was skipped
        Assert.Equal(16384, highProfile.Sampler.ContextTokens);  // Int was preserved
    }

    [Fact]
    public void MapToProfiles_WithOutOfRangeTemperature_IncludesValueWithoutValidation()
    {
        // Arrange - temperature > 2 (typical valid range is 0-2)
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
                    Sampler: new Dictionary<string, object> { { "temperature", 5.0 } }),
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
            },
            Hardware: new HardwareAssessment("Test CPU", 8, "TestMachine"));

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert - out-of-range values are included without validation (mapper doesn't validate ranges)
        var highProfile = profiles.First(p => p.Tier == "high");
        Assert.NotNull(highProfile.Sampler);
        Assert.Equal(5.0, highProfile.Sampler!.Temperature);
    }

    [Fact]
    public void MapToProfiles_WithNegativeTemperature_IncludesValueWithoutValidation()
    {
        // Arrange - negative temperature (definitely invalid, but mapper doesn't validate)
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
                    Sampler: new Dictionary<string, object> { { "temperature", -1.5 } }),
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
            },
            Hardware: new HardwareAssessment("Test CPU", 8, "TestMachine"));

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert
        var highProfile = profiles.First(p => p.Tier == "high");
        Assert.NotNull(highProfile.Sampler);
        Assert.Equal(-1.5, highProfile.Sampler!.Temperature);
    }

    [Fact]
    public void MapToProfiles_WithOutOfRangeTopP_IncludesValueWithoutValidation()
    {
        // Arrange - topP > 1 (typical valid range is 0-1)
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
                    Sampler: new Dictionary<string, object> { { "topP", 1.5 } }),
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
            },
            Hardware: new HardwareAssessment("Test CPU", 8, "TestMachine"));

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert
        var highProfile = profiles.First(p => p.Tier == "high");
        Assert.NotNull(highProfile.Sampler);
        Assert.Equal(1.5, highProfile.Sampler!.TopP);
    }

    [Fact]
    public void MapToProfiles_WithNegativeTopK_IncludesValueWithoutValidation()
    {
        // Arrange - negative topK (invalid but mapper doesn't validate)
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
                    Sampler: new Dictionary<string, object> { { "topK", -10 } }),
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
            },
            Hardware: new HardwareAssessment("Test CPU", 8, "TestMachine"));

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert
        var highProfile = profiles.First(p => p.Tier == "high");
        Assert.NotNull(highProfile.Sampler);
        Assert.Equal(-10, highProfile.Sampler!.TopK);
    }

    [Fact]
    public void MapToProfiles_WithZeroContextTokens_IsSkipped()
    {
        // Arrange - contextTokens = 0 (invalid)
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
                    Sampler: new Dictionary<string, object> { { "contextTokens", 0 } }),
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
            },
            Hardware: new HardwareAssessment("Test CPU", 8, "TestMachine"));

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert - ProfilingRunner skips contextTokens if MaxContextWindow <= 0
        var highProfile = profiles.First(p => p.Tier == "high");
        // Sampler may exist but contextTokens should not be set
        if (highProfile.Sampler != null)
        {
            Assert.Null(highProfile.Sampler.ContextTokens);  // 0 should be skipped or null
        }
    }

    [Fact]
    public void MapToProfiles_WithHardwareNull_UsesDefaults()
    {
        // Arrange - null hardware
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
            },
            Hardware: null);  // Null hardware

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert - RecommendationMapper should use "Unknown" defaults
        var highProfile = profiles.First(p => p.Tier == "high");
        Assert.Equal("Unknown", highProfile.Hardware.Chip);
        Assert.Equal(0, highProfile.Hardware.MemoryGb);
        Assert.Equal("Unknown", highProfile.Hardware.ModelIdentifier);
    }

    [Fact]
    public void MapToProfiles_WithZeroMemoryHardware_PreservesZero()
    {
        // Arrange - hardware with zero memory
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
            },
            Hardware: new HardwareAssessment("ARM64", 0, "RaspberryPi"));

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert
        var highProfile = profiles.First(p => p.Tier == "high");
        Assert.Equal("ARM64", highProfile.Hardware.Chip);
        Assert.Equal(0, highProfile.Hardware.MemoryGb);
        Assert.Equal("RaspberryPi", highProfile.Hardware.ModelIdentifier);
    }

    [Fact]
    public void MapToProfiles_WithEmptyNullSettingsDictionaries_HandlesGracefully()
    {
        // Arrange - empty or null value in settings
        var manifest = new RecommendationManifest(
            ModelHfId: "test/model",
            AssessmentVersion: "1.0.0",
            Timestamp: DateTime.UtcNow.ToString("O"),
            Recommendations: new Dictionary<string, TierRecommendation>
            {
                ["high"] = new TierRecommendation(
                    Tier: "high",
                    System: new Dictionary<string, object>(),  // Empty system settings
                    Omlx: new Dictionary<string, object>(),  // Empty omlx
                    Harness: new Dictionary<string, object>(),  // Empty harness
                    Sampler: null),
                ["balanced"] = new TierRecommendation(
                    Tier: "balanced",
                    System: new Dictionary<string, object> { { "os", "macOS" } },  // Non-null value
                    Omlx: new Dictionary<string, object>(),
                    Harness: new Dictionary<string, object> { { "framework", "vscode" } },  // Non-null value
                    Sampler: null),
                ["efficient"] = new TierRecommendation(
                    Tier: "efficient",
                    System: new Dictionary<string, object> { { "os", "Linux" } },
                    Omlx: new Dictionary<string, object> { { "compute_units", "ALL" } },  // Non-null value
                    Harness: new Dictionary<string, object> { { "framework", "vscode" } },
                    Sampler: null)
            },
            Hardware: new HardwareAssessment("Test CPU", 8, "TestMachine"));

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert - should handle null/empty values gracefully
        Assert.Equal(3, profiles.Count);
        var highProfile = profiles.First(p => p.Tier == "high");
        Assert.Empty(highProfile.System);  // Null system becomes empty
        Assert.Empty(highProfile.OMLXSettings);
        Assert.Empty(highProfile.Harness);
    }

    [Fact]
    public void MapToProfiles_WithMixedValidInvalidSamplerValues_PreservesValidOnly()
    {
        // Arrange - mix of valid and invalid sampler values
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
                    Sampler: new Dictionary<string, object>
                    {
                        { "temperature", 0.7 },      // Valid double
                        { "topP", "invalid" },       // Invalid string
                        { "topK", 40 },              // Valid int
                        { "repetitionPenalty", 1.2 }, // Valid double
                        { "contextTokens", 8192 }     // Valid int
                    }),
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
            },
            Hardware: new HardwareAssessment("Test CPU", 8, "TestMachine"));

        // Act
        var profiles = _mapper.MapToProfiles(manifest);

        // Assert - valid values are kept, invalid ones are skipped
        var highProfile = profiles.First(p => p.Tier == "high");
        Assert.NotNull(highProfile.Sampler);
        Assert.Equal(0.7, highProfile.Sampler!.Temperature);      // Valid
        Assert.Null(highProfile.Sampler.TopP);                     // Skipped (string)
        Assert.Equal(40, highProfile.Sampler.TopK);                // Valid
        Assert.Equal(1.2, highProfile.Sampler.RepetitionPenalty);  // Valid
        Assert.Equal(8192, highProfile.Sampler.ContextTokens);     // Valid
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
