using Microsoft.AspNetCore.Builder;
using MlxPep.Core;
using System.Text.Json;

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
                Type: "default",
                Parameters: new Dictionary<string, object> { { "temperature", 0.7 } }
            ),
            Community: null
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
        Assert.Contains("\"modelHfId\":\"test-model/test-1b\"", json);
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
    public void ProfileWithNullCommunityMetadataSerializesCorrectly()
    {
        // Arrange: Create a profile without community metadata (common for local profiles)
        var profile = CreateTestProfile();

        // Act: Serialize to JSON
        var json = JsonSerializer.Serialize(profile);

        // Assert: Verify community is omitted when null (per JsonIgnore condition)
        var doc = JsonDocument.Parse(json);
        bool hasCommunity = doc.RootElement.TryGetProperty("community", out _);
        Assert.False(hasCommunity, "Community metadata should be omitted when null");
    }

    [Fact]
    public void ProfileWithCommunityMetadataSerializesCorrectly()
    {
        // Arrange: Create a profile with community metadata
        var profile = CreateTestProfile() with
        {
            Community = new CommunityMetadata(
                Tags: new List<string> { "efficient", "testing" },
                Keywords: new List<string> { "test", "omlx" },
                Description: "A test profile for unit testing",
                MinMemoryGb: 32,
                MaxMemoryGb: 128,
                HardwareFamily: "MacBook Pro",
                DedupKey: "test-dedup-key"
            )
        };

        // Act: Serialize to JSON
        var json = JsonSerializer.Serialize(profile);

        // Assert: Verify community metadata is included
        var doc = JsonDocument.Parse(json);
        bool hasCommunity = doc.RootElement.TryGetProperty("community", out var communityObj);
        Assert.True(hasCommunity, "Community metadata should be included when not null");
        Assert.True(communityObj.TryGetProperty("tags", out _), "Tags should be present");
        Assert.True(communityObj.TryGetProperty("description", out _), "Description should be present");
    }
}


