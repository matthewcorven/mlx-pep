namespace MlxPep.Core.Tests;

using System;
using System.Collections.Generic;
using Xunit;

/// <summary>
/// Basic tests for Profile record structure.
/// Comprehensive issue #8 tests are in Issue8ProfileSchemaTests.cs
/// Issue #27 (community metadata) tests are deferred.
/// </summary>
public class ProfileTests
{
    private static Profile CreateTestProfile()
    {
        return new Profile(
            SchemaVersion: 1,
            Id: "test-profile-001",
            ModelHfId: "meta-llama/Llama-2-7b",
            Tier: "balanced",
            Engine: "mlx",
            System: new Dictionary<string, object> { { "os", "macOS" } },
            OMLXSettings: new Dictionary<string, object> { { "compute_units", "ALL" } },
            Harness: new Dictionary<string, object> { { "backend", "mlx" } },
            Provenance: new ProfileProvenance(
                Author: "test-author",
                CreatedAt: DateTime.UtcNow.ToString("O"),
                Source: "test"
            ),
            Hardware: new HardwareFingerprint(
                Chip: "Apple M2",
                MemoryGb: 16,
                ModelIdentifier: "MacBookPro18,2"
            ),
            Sampler: null
        );
    }

    [Fact]
    public void Profile_CreateTestProfile_Succeeds()
    {
        // Arrange & Act
        var profile = CreateTestProfile();

        // Assert
        Assert.NotNull(profile);
        Assert.Equal(1, profile.SchemaVersion);
        Assert.Equal("test-profile-001", profile.Id);
        Assert.Equal("balanced", profile.Tier);
    }

    [Fact]
    public void Profile_TierValues_AreValid()
    {
        // Test that tier values match specification
        var validTiers = new[] { "high", "balanced", "efficient" };

        foreach (var tier in validTiers)
        {
            var profile = CreateTestProfile() with { Tier = tier };
            Assert.Equal(tier, profile.Tier);
        }
    }

    [Fact]
    public void Profile_ProvenanceRequired()
    {
        // Verify provenance is populated
        var profile = CreateTestProfile();

        Assert.NotNull(profile.Provenance);
        Assert.Equal("test-author", profile.Provenance.Author);
        Assert.NotEmpty(profile.Provenance.CreatedAt);
        Assert.Equal("test", profile.Provenance.Source);
    }

    [Fact]
    public void Profile_HardwareRequired()
    {
        // Verify hardware fingerprint is populated
        var profile = CreateTestProfile();

        Assert.NotNull(profile.Hardware);
        Assert.Equal("Apple M2", profile.Hardware.Chip);
        Assert.Equal(16, profile.Hardware.MemoryGb);
        Assert.Equal("MacBookPro18,2", profile.Hardware.ModelIdentifier);
    }

    [Fact]
    public void Profile_SamplerOptional()
    {
        // Verify sampler can be null
        var profile = CreateTestProfile();
        Assert.Null(profile.Sampler);

        // And can be set
        var profileWithSampler = profile with
        {
            Sampler = new SamplerSettings(0.7, null, null, null, null)
        };
        Assert.NotNull(profileWithSampler.Sampler);
        Assert.Equal(0.7, profileWithSampler.Sampler.Temperature);
    }
}
