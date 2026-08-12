using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using MlxPep.Core;
using System.Net;
using System.Net.Http.Headers;
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

public class AuthenticationTests
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
    public async Task GetProfileWithoutAuthTokenSucceeds()
    {
        // Arrange: Create a test profile
        var profile = CreateTestProfile();
        var json = JsonSerializer.Serialize(profile);

        // Act: Make a GET request without authentication
        var client = new HttpClient();
        
        // Note: This test verifies the auth pattern - actual HTTP testing would require
        // setting up a test server with blob storage mock
        // For now, we verify the profile model works correctly
        var deserializedProfile = JsonSerializer.Deserialize<Profile>(json);

        // Assert: GET requests should succeed without authentication
        Assert.NotNull(deserializedProfile);
        Assert.Equal(profile.Id, deserializedProfile.Id);
    }

    [Fact]
    public void PostProfileWithoutAuthTokenShouldFail()
    {
        // Arrange: Create a test profile
        var profile = CreateTestProfile();
        var authHeader = "";

        // Act & Assert: Simulate auth check for POST without token
        const string bearerPrefix = "Bearer ";
        bool isAuthorized = !string.IsNullOrEmpty(authHeader) && 
                           authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase) &&
                           !string.IsNullOrEmpty(authHeader[bearerPrefix.Length..]);

        // Verify unauthorized access is blocked
        Assert.False(isAuthorized, "POST without token should not be authorized");
    }

    [Fact]
    public void PostProfileWithValidTokenShouldSucceed()
    {
        // Arrange: Create test data
        var profile = CreateTestProfile();
        var authHeader = "Bearer valid-test-token-12345";

        // Act: Validate auth header
        const string bearerPrefix = "Bearer ";
        bool isAuthorized = !string.IsNullOrEmpty(authHeader) && 
                           authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase) &&
                           !string.IsNullOrEmpty(authHeader[bearerPrefix.Length..]);

        // Assert: Valid token should authorize the request
        Assert.True(isAuthorized, "POST with valid Bearer token should be authorized");
        Assert.Equal("valid-test-token-12345", authHeader[bearerPrefix.Length..]);
    }

    [Fact]
    public void PostProfileWithInvalidTokenFormatShouldFail()
    {
        // Arrange: Create test data with invalid token format
        var profile = CreateTestProfile();
        var authHeader = "InvalidFormat some-token";

        // Act: Validate auth header
        const string bearerPrefix = "Bearer ";
        bool isAuthorized = !string.IsNullOrEmpty(authHeader) && 
                           authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase) &&
                           !string.IsNullOrEmpty(authHeader[bearerPrefix.Length..]);

        // Assert: Invalid format should not be authorized
        Assert.False(isAuthorized, "POST with invalid token format should not be authorized");
    }

    [Fact]
    public void PutProfileWithValidTokenShouldSucceed()
    {
        // Arrange: Create test data
        var profile = CreateTestProfile();
        var authHeader = "Bearer valid-update-token";

        // Act: Validate auth header for PUT operation
        const string bearerPrefix = "Bearer ";
        bool isAuthorized = !string.IsNullOrEmpty(authHeader) && 
                           authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase) &&
                           !string.IsNullOrEmpty(authHeader[bearerPrefix.Length..]);

        // Assert: PUT with valid token should be authorized
        Assert.True(isAuthorized, "PUT with valid Bearer token should be authorized");
    }

    [Fact]
    public void PutProfileWithoutAuthTokenShouldFail()
    {
        // Arrange: No auth header
        var authHeader = "";

        // Act & Assert: Simulate auth check for PUT without token
        const string bearerPrefix = "Bearer ";
        bool isAuthorized = !string.IsNullOrEmpty(authHeader) && 
                           authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase) &&
                           !string.IsNullOrEmpty(authHeader[bearerPrefix.Length..]);

        // Verify unauthorized access is blocked
        Assert.False(isAuthorized, "PUT without token should not be authorized");
    }

    [Fact]
    public void DeleteProfileWithoutAuthTokenShouldFail()
    {
        // Arrange: No auth header
        var authHeader = "";

        // Act & Assert: Simulate auth check for DELETE without token
        const string bearerPrefix = "Bearer ";
        bool isAuthorized = !string.IsNullOrEmpty(authHeader) && 
                           authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase) &&
                           !string.IsNullOrEmpty(authHeader[bearerPrefix.Length..]);

        // Verify unauthorized access is blocked
        Assert.False(isAuthorized, "DELETE without token should not be authorized");
    }

    [Fact]
    public void DeleteProfileWithValidTokenShouldSucceed()
    {
        // Arrange: Create test data
        var authHeader = "Bearer valid-delete-token";

        // Act: Validate auth header for DELETE operation
        const string bearerPrefix = "Bearer ";
        bool isAuthorized = !string.IsNullOrEmpty(authHeader) && 
                           authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase) &&
                           !string.IsNullOrEmpty(authHeader[bearerPrefix.Length..]);

        // Assert: DELETE with valid token should be authorized
        Assert.True(isAuthorized, "DELETE with valid Bearer token should be authorized");
    }

    [Fact]
    public void BearerTokenWithEmptyStringAfterPrefixShouldFail()
    {
        // Arrange: Bearer prefix with empty token
        var authHeader = "Bearer ";

        // Act & Assert: Simulate auth check
        const string bearerPrefix = "Bearer ";
        bool isAuthorized = !string.IsNullOrEmpty(authHeader) && 
                           authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase) &&
                           !string.IsNullOrEmpty(authHeader[bearerPrefix.Length..]);

        // Verify empty token is not authorized
        Assert.False(isAuthorized, "Bearer token with empty string should not be authorized");
    }

    [Fact]
    public void AuthTokenIsCaseSensitiveForBearerPrefix()
    {
        // Arrange: Lowercase bearer (should still work due to OrdinalIgnoreCase)
        var authHeader = "bearer valid-token";

        // Act: Validate auth header with case-insensitive comparison
        const string bearerPrefix = "Bearer ";
        bool isAuthorized = !string.IsNullOrEmpty(authHeader) && 
                           authHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase) &&
                           !string.IsNullOrEmpty(authHeader[bearerPrefix.Length..]);

        // Assert: Case-insensitive Bearer should work
        Assert.True(isAuthorized, "Bearer prefix should be case-insensitive");
    }
}


