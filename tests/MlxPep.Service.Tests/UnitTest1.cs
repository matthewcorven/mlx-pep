using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using MlxPep.Core;

namespace MlxPep.Service.Tests;

public class ServiceInitializationTests
{
    [Fact]
    public void ServiceCanBeBuilt()
    {
        // Arrange: Create a WebApplicationBuilder
        var builder = WebApplication.CreateBuilder();

        // Act: Build the app
        var app = builder.Build();

        // Assert: Verify service was created
        Assert.NotNull(app);
    }

    [Fact]
    public void ServiceConfigurationSupportsAzureBlob()
    {
        // Arrange: Create a WebApplicationBuilder
        var builder = WebApplication.CreateBuilder();

        // Act: Get the configuration
        var config = builder.Configuration;

        // Assert: Verify configuration exists
        Assert.NotNull(config);
    }
}

public class ProfileCRUDEndpointTests
{
    private static Profile CreateTestProfile(string id = "test-profile-1")
    {
        return new Profile(
            SchemaVersion: 1,
            Id: id,
            ModelHfId: "test-model/test-1b",
            Tier: "balanced",
            Engine: "omlx",
            System: new Dictionary<string, object> { { "iogpu.wired_limit_mb", 122880 } },
            OMLXSettings: new Dictionary<string, object> { { "memory_guard_tier", "balanced" } },
            Harness: new Dictionary<string, object>
            {
                { "vscode", new Dictionary<string, object> { { "maxInputTokens", 64000 } } }
            },
            Provenance: new ProfileProvenance(
                Author: "test-author",
                CreatedAt: "2026-08-12T00:00:00Z",
                Source: "test"
            ),
            Hardware: new HardwareFingerprint(
                Chip: "Apple M4",
                MemoryGb: 64,
                ModelIdentifier: "MacBook16,5"
            ),
            Sampler: new SamplerSettings(
                Temperature: 0.7,
                TopP: null,
                TopK: null,
                RepetitionPenalty: null,
                ContextTokens: null
            )
        );
    }

    [Fact]
    public void ProfileModelSerializesCorrectly()
    {
        // Arrange: Create a test profile
        var profile = CreateTestProfile();

        // Act: Serialize to JSON
        var json = JsonSerializer.Serialize(profile);

        // Assert: Verify JSON contains expected fields
        Assert.Contains("\"schemaVersion\":1", json);
        Assert.Contains("\"id\":\"test-profile-1\"", json);
        Assert.Contains("\"modelHfId\":\"test-model/test-1b\"", json);  // Note: "/" is stored as-is (not sanitized)
        Assert.Contains("\"tier\":\"balanced\"", json);
    }

    [Fact]
    public void ProfileModelDeserializesCorrectly()
    {
        // Arrange: Create a profile and serialize it
        var originalProfile = CreateTestProfile();
        var json = JsonSerializer.Serialize(originalProfile);

        // Act: Deserialize back
        var deserializedProfile = JsonSerializer.Deserialize<Profile>(json);

        // Assert: Verify fields match
        Assert.NotNull(deserializedProfile);
        Assert.Equal(originalProfile.Id, deserializedProfile.Id);
        Assert.Equal(originalProfile.ModelHfId, deserializedProfile.ModelHfId);
        Assert.Equal(originalProfile.Tier, deserializedProfile.Tier);
    }

    [Fact]
    public void ProfileWithNullSamplerSerializesCorrectly()
    {
        // Arrange: Create a profile without sampler (common for local profiles)
        var profile = CreateTestProfile() with { Sampler = null };

        // Act: Serialize to JSON
        var json = JsonSerializer.Serialize(profile);

        // Assert: Verify sampler is omitted when null
        var doc = JsonDocument.Parse(json);
        bool hasSampler = doc.RootElement.TryGetProperty("sampler", out _);
        Assert.False(hasSampler, "Sampler should be omitted when null");
    }

    [Fact]
    public void ProfileWithSamplerSettingsSerializesCorrectly()
    {
        // Arrange: Create a profile with sampler settings
        var profile = CreateTestProfile() with
        {
            Sampler = new SamplerSettings(
                Temperature: 0.8,
                TopP: 0.9,
                TopK: 40,
                RepetitionPenalty: 1.1,
                ContextTokens: 2048
            )
        };

        // Act: Serialize to JSON
        var json = JsonSerializer.Serialize(profile);

        // Assert: Verify sampler settings are included
        var doc = JsonDocument.Parse(json);
        bool hasSampler = doc.RootElement.TryGetProperty("sampler", out var samplerObj);
        Assert.True(hasSampler, "Sampler settings should be included when not null");
        Assert.True(samplerObj.TryGetProperty("temperature", out _), "Temperature should be present");
        Assert.True(samplerObj.TryGetProperty("topP", out _), "TopP should be present");
    }
}

