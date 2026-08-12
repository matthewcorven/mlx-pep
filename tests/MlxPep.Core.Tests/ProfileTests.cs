namespace MlxPep.Core.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Comprehensive tests for Profile, ProfileValidator, and ProfileReader.
/// Issue #27: profiling: publish-flow polish + community metadata
/// </summary>
public class ProfileTests
{
    private static Profile CreateTestProfile(CommunityMetadata? community = null)
    {
        return new Profile(
            SchemaVersion: 1,
            Id: "test-profile-001",
            ModelHfId: "meta-llama/Llama-2-7b",
            Tier: "experimental",
            Engine: "mlx",
            System: new Dictionary<string, object> { { "os", "macOS" } },
            OMLXSettings: new Dictionary<string, object> { { "compute_units", "ALL" } },
            Harness: new Dictionary<string, object> { { "backend", "mlx" } },
            Provenance: new ProfileProvenance(
                Author: "test-author",
                CreatedAt: DateTime.UtcNow.ToIso8601String(),
                Source: "community"
            ),
            Hardware: new HardwareFingerprint(
                Chip: "Apple M2",
                MemoryGb: 16,
                ModelIdentifier: "MacBookPro18,2"
            ),
            Sampler: null,
            Community: community
        );
    }

    [Fact]
    public void ProfileValidator_ValidateForPublishing_MissingCommunityMetadata_Fails()
    {
        // Arrange
        var validator = new ProfileValidator();
        var profile = CreateTestProfile(community: null);

        // Act
        var result = validator.ValidateForPublishing(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Community metadata is required", string.Join("; ", result.Errors));
    }

    [Fact]
    public void ProfileValidator_ValidateForPublishing_MissingDedupKey_Fails()
    {
        // Arrange
        var validator = new ProfileValidator();
        var community = new CommunityMetadata(
            Tags: new List<string> { "production" },
            Keywords: new List<string> { "llama" },
            Description: "A test profile",
            MinMemoryGb: 8,
            MaxMemoryGb: 32,
            HardwareFamily: "Apple Silicon",
            DedupKey: null
        );
        var profile = CreateTestProfile(community: community);

        // Act
        var result = validator.ValidateForPublishing(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("dedupKey", string.Join("; ", result.Errors));
    }

    [Fact]
    public void ProfileValidator_ValidateForPublishing_InvalidDedupKey_Fails()
    {
        // Arrange
        var validator = new ProfileValidator();
        var community = new CommunityMetadata(
            Tags: new List<string> { "production" },
            Description: "Test",
            DedupKey: "ab"  // Too short
        );
        var profile = CreateTestProfile(community: community);

        // Act
        var result = validator.ValidateForPublishing(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("alphanumeric", string.Join("; ", result.Errors));
    }

    [Fact]
    public void ProfileValidator_ValidateForPublishing_ValidProfile_Succeeds()
    {
        // Arrange
        var validator = new ProfileValidator();
        var community = new CommunityMetadata(
            Tags: new List<string> { "production", "inference" },
            Keywords: new List<string> { "llama", "2-7b" },
            Description: "Optimized Llama 2 7B profile for Apple Silicon",
            MinMemoryGb: 8,
            MaxMemoryGb: 32,
            HardwareFamily: "Apple Silicon",
            DedupKey: "llama-2-7b-apple-m2"
        );
        var profile = CreateTestProfile(community: community);

        // Act
        var result = validator.ValidateForPublishing(profile);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ProfileValidator_ValidateForPublishing_InvalidMemoryRange_Fails()
    {
        // Arrange
        var validator = new ProfileValidator();
        var community = new CommunityMetadata(
            Description: "Test",
            MinMemoryGb: 32,
            MaxMemoryGb: 8,  // Min > Max
            DedupKey: "test-profile"
        );
        var profile = CreateTestProfile(community: community);

        // Act
        var result = validator.ValidateForPublishing(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Minimum memory cannot exceed maximum", string.Join("; ", result.Errors));
    }

    [Fact]
    public void ProfileValidator_ValidateForPublishing_DescriptionTooLong_Fails()
    {
        // Arrange
        var validator = new ProfileValidator();
        var longDescription = new string('x', 501);  // 501 chars, exceeds 500 limit
        var community = new CommunityMetadata(
            Description: longDescription,
            DedupKey: "test-profile"
        );
        var profile = CreateTestProfile(community: community);

        // Act
        var result = validator.ValidateForPublishing(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Description cannot exceed 500 characters", string.Join("; ", result.Errors));
    }

    [Fact]
    public void ProfileValidator_ValidateForPublishing_InvalidTags_Fails()
    {
        // Arrange
        var validator = new ProfileValidator();
        var community = new CommunityMetadata(
            Tags: new List<string> { "production", "invalid-tag-xyz" },
            DedupKey: "test-profile"
        );
        var profile = CreateTestProfile(community: community);

        // Act
        var result = validator.ValidateForPublishing(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Invalid tags", string.Join("; ", result.Errors));
    }

    [Fact]
    public async Task ProfileReader_SerializationRoundTrip_PreservesData()
    {
        // Arrange
        var community = new CommunityMetadata(
            Tags: new List<string> { "production" },
            Keywords: new List<string> { "llama" },
            Description: "Test profile",
            MinMemoryGb: 8,
            MaxMemoryGb: 32,
            HardwareFamily: "Apple Silicon",
            DedupKey: "test-dedup"
        );
        var originalProfile = CreateTestProfile(community: community);
        var reader = new ProfileReader();
        var tempFile = Path.GetTempFileName() + ".jsonl";

        try
        {
            // Act: Write and read back
            await reader.WriteProfileSetAsync(tempFile, new List<Profile> { originalProfile });
            var readProfiles = await reader.ReadProfileSetAsync(tempFile);

            // Assert
            Assert.Single(readProfiles);
            var readProfile = readProfiles[0];

            Assert.Equal(originalProfile.Id, readProfile.Id);
            Assert.Equal(originalProfile.ModelHfId, readProfile.ModelHfId);

            Assert.NotNull(readProfile.Community);
            Assert.Equal("test-dedup", readProfile.Community.DedupKey);
            Assert.NotNull(readProfile.Community.Keywords);
            Assert.Equal("llama", readProfile.Community.Keywords[0]);
            Assert.Equal(8, readProfile.Community.MinMemoryGb);
            Assert.Equal(32, readProfile.Community.MaxMemoryGb);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ProfileReader_FindDuplicatesByDedupKey_IdentifiesDuplicates()
    {
        // Arrange
        var reader = new ProfileReader();
        var profiles = new List<Profile>
        {
            CreateTestProfile(new CommunityMetadata(DedupKey: "llama-2-7b", Description: "v1")),
            CreateTestProfile(new CommunityMetadata(DedupKey: "llama-2-7b", Description: "v2")),
            CreateTestProfile(new CommunityMetadata(DedupKey: "mistral-7b", Description: "v1")),
        };

        // Act
        var duplicates = reader.FindDuplicatesByDedupKey(profiles);

        // Assert
        Assert.Single(duplicates);
        Assert.Contains("llama-2-7b", duplicates.Keys);
        Assert.Equal(2, duplicates["llama-2-7b"].Count);
    }

    [Fact]
    public void ProfileReader_DeduplicateByDedupKey_KeepsNewest()
    {
        // Arrange
        var reader = new ProfileReader();
        var now = DateTime.UtcNow;
        var olderTime = now.AddDays(-1).ToIso8601String();
        var newerTime = now.ToIso8601String();

        var profiles = new List<Profile>
        {
            new Profile(
                SchemaVersion: 1,
                Id: "id-1",
                ModelHfId: "model-1",
                Tier: "exp",
                Engine: "mlx",
                System: new Dictionary<string, object>(),
                OMLXSettings: new Dictionary<string, object>(),
                Harness: new Dictionary<string, object>(),
                Provenance: new ProfileProvenance("author", olderTime, "community"),
                Hardware: new HardwareFingerprint("chip", 16, "model"),
                Community: new CommunityMetadata(DedupKey: "shared-key")
            ),
            new Profile(
                SchemaVersion: 1,
                Id: "id-2",
                ModelHfId: "model-1",
                Tier: "exp",
                Engine: "mlx",
                System: new Dictionary<string, object>(),
                OMLXSettings: new Dictionary<string, object>(),
                Harness: new Dictionary<string, object>(),
                Provenance: new ProfileProvenance("author", newerTime, "community"),
                Hardware: new HardwareFingerprint("chip", 16, "model"),
                Community: new CommunityMetadata(DedupKey: "shared-key")
            ),
        };

        // Act
        var deduped = reader.DeduplicateByDedupKey(profiles);

        // Assert
        Assert.Single(deduped);
        Assert.Equal("id-2", deduped[0].Id);  // Newer one retained
    }

    [Fact]
    public void ProfileReader_SearchProfiles_FindsByDescription()
    {
        // Arrange
        var reader = new ProfileReader();
        var profiles = new List<Profile>
        {
            CreateTestProfile(new CommunityMetadata(
                Description: "Optimized for inference"
            )),
            CreateTestProfile(new CommunityMetadata(
                Description: "Training profile"
            )),
        };

        // Act
        var results = reader.SearchProfiles(profiles, "inference");

        // Assert
        Assert.Single(results);
        Assert.Contains("inference", results[0].Community?.Description ?? "");
    }

    [Fact]
    public void ProfileReader_SearchProfiles_FindsByTags()
    {
        // Arrange
        var reader = new ProfileReader();
        var profiles = new List<Profile>
        {
            CreateTestProfile(new CommunityMetadata(
                Tags: new List<string> { "production", "gpu" }
            )),
            CreateTestProfile(new CommunityMetadata(
                Tags: new List<string> { "experimental" }
            )),
        };

        // Act
        var results = reader.SearchProfiles(profiles, "production");

        // Assert
        Assert.Single(results);
        Assert.Contains("production", results[0].Community?.Tags ?? new List<string>());
    }

    [Fact]
    public void ProfileReader_FilterByHardware_RespectMemoryRange()
    {
        // Arrange
        var reader = new ProfileReader();
        var profiles = new List<Profile>
        {
            CreateTestProfile(new CommunityMetadata(
                MinMemoryGb: 8,
                MaxMemoryGb: 16
            )),
            CreateTestProfile(new CommunityMetadata(
                MinMemoryGb: 32,
                MaxMemoryGb: 64
            )),
        };

        // Act
        var results16gb = reader.FilterByHardware(profiles, 16);
        var results32gb = reader.FilterByHardware(profiles, 32);
        var results50gb = reader.FilterByHardware(profiles, 50);

        // Assert
        Assert.Single(results16gb);           // 16GB only fits first range (8-16)
        Assert.Single(results32gb);           // 32GB only fits second range (32-64)
        Assert.Single(results50gb);           // 50GB only fits second range (32-64)
    }

    [Fact]
    public void ProfileReader_FilterPublishable_OnlyIncludesWithCommunity()
    {
        // Arrange
        var reader = new ProfileReader();
        var profiles = new List<Profile>
        {
            CreateTestProfile(community: null),
            CreateTestProfile(new CommunityMetadata(DedupKey: "test")),
            CreateTestProfile(community: null),
            CreateTestProfile(new CommunityMetadata(DedupKey: "test2")),
        };

        // Act
        var publishable = reader.FilterPublishable(profiles);

        // Assert
        Assert.Equal(2, publishable.Count);
        Assert.All(publishable, p => Assert.NotNull(p.Community));
    }
}

/// <summary>
/// Extension methods for testing.
/// </summary>
public static class TestExtensions
{
    public static string ToIso8601String(this DateTime dt)
    {
        return dt.ToUniversalTime().ToString("O");
    }
}
