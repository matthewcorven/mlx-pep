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

    [Fact]
    public void ProfileValidator_ValidateForLocalUse_ValidProfile_Succeeds()
    {
        // Arrange
        var validator = new ProfileValidator();
        var profile = CreateTestProfile(community: null);

        // Act
        var result = validator.ValidateForLocalUse(profile);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ProfileValidator_ValidateForLocalUse_MissingEngine_Fails()
    {
        // Arrange
        var validator = new ProfileValidator();
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-profile-001",
            ModelHfId: "meta-llama/Llama-2-7b",
            Tier: "experimental",
            Engine: "",  // Empty engine
            System: new Dictionary<string, object> { { "os", "macOS" } },
            OMLXSettings: new Dictionary<string, object> { { "compute_units", "ALL" } },
            Harness: new Dictionary<string, object> { { "backend", "mlx" } },
            Provenance: new ProfileProvenance("test-author", DateTime.UtcNow.ToIso8601String(), "community"),
            Hardware: new HardwareFingerprint("Apple M2", 16, "MacBookPro18,2"),
            Sampler: null,
            Community: null
        );

        // Act
        var result = validator.ValidateForLocalUse(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Engine is required", string.Join("; ", result.Errors));
    }

    [Fact]
    public void ProfileValidator_ValidateForLocalUse_UnsupportedEngine_Fails()
    {
        // Arrange
        var validator = new ProfileValidator();
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-profile-001",
            ModelHfId: "meta-llama/Llama-2-7b",
            Tier: "experimental",
            Engine: "unsupported-engine",
            System: new Dictionary<string, object> { { "os", "macOS" } },
            OMLXSettings: new Dictionary<string, object> { { "compute_units", "ALL" } },
            Harness: new Dictionary<string, object> { { "backend", "mlx" } },
            Provenance: new ProfileProvenance("test-author", DateTime.UtcNow.ToIso8601String(), "community"),
            Hardware: new HardwareFingerprint("Apple M2", 16, "MacBookPro18,2"),
            Sampler: null,
            Community: null
        );

        // Act
        var result = validator.ValidateForLocalUse(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Unsupported engine", string.Join("; ", result.Errors));
    }

    [Fact]
    public void RuntimeEngineRegistry_GetEngine_ReturnsCorrectEngine()
    {
        // Arrange
        var registry = new RuntimeEngineRegistry();

        // Act & Assert
        Assert.NotNull(registry.GetEngine("omlx"));
        Assert.NotNull(registry.GetEngine("mlx-lm"));
        Assert.NotNull(registry.GetEngine("llama.cpp"));
        Assert.NotNull(registry.GetEngine("vllm"));
        Assert.Null(registry.GetEngine("unknown"));
    }

    [Fact]
    public void RuntimeEngineRegistry_IsSupported_ChecksEngineAvailability()
    {
        // Arrange
        var registry = new RuntimeEngineRegistry();

        // Act & Assert
        Assert.True(registry.IsSupported("omlx"));
        Assert.True(registry.IsSupported("mlx-lm"));
        Assert.True(registry.IsSupported("llama.cpp"));
        Assert.True(registry.IsSupported("vllm"));
        Assert.False(registry.IsSupported("unsupported"));
    }

    [Fact]
    public void OMLXEngine_ValidateSettings_RequiresOMLXSettings()
    {
        // Arrange
        var engine = new OMLXEngine();
        var profileWithoutSettings = new Profile(
            SchemaVersion: 1,
            Id: "test",
            ModelHfId: "model",
            Tier: "exp",
            Engine: "omlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),  // Empty
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("author", DateTime.UtcNow.ToIso8601String(), "source"),
            Hardware: new HardwareFingerprint("chip", 16, "model")
        );

        // Act
        var result = engine.ValidateSettings(profileWithoutSettings);

        // Assert - empty dict should fail
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ProfileReader_FilterByEngine_ReturnsCorrectProfiles()
    {
        // Arrange
        var reader = new ProfileReader();
        var profiles = new List<Profile>
        {
            CreateTestProfile(),  // Engine: "mlx"
            new Profile(
                SchemaVersion: 1,
                Id: "llama-cpp-profile",
                ModelHfId: "meta-llama/Llama-2-13b",
                Tier: "production",
                Engine: "llama.cpp",
                System: new Dictionary<string, object>(),
                OMLXSettings: new Dictionary<string, object>(),
                Harness: new Dictionary<string, object>(),
                Provenance: new ProfileProvenance("author", DateTime.UtcNow.ToIso8601String(), "community"),
                Hardware: new HardwareFingerprint("Intel", 32, "standard")
            ),
            new Profile(
                SchemaVersion: 1,
                Id: "vllm-profile",
                ModelHfId: "meta-llama/Llama-2-70b",
                Tier: "production",
                Engine: "vllm",
                System: new Dictionary<string, object>(),
                OMLXSettings: new Dictionary<string, object>(),
                Harness: new Dictionary<string, object>(),
                Provenance: new ProfileProvenance("author", DateTime.UtcNow.ToIso8601String(), "community"),
                Hardware: new HardwareFingerprint("GPU", 80, "gpu-cluster")
            ),
        };

        // Act
        var mlxProfiles = reader.FilterByEngine(profiles, "mlx");
        var llamaCppProfiles = reader.FilterByEngine(profiles, "llama.cpp");
        var vllmProfiles = reader.FilterByEngine(profiles, "vllm");

        // Assert
        Assert.Single(mlxProfiles);
        Assert.Single(llamaCppProfiles);
        Assert.Single(vllmProfiles);
        Assert.Equal("test-profile-001", mlxProfiles[0].Id);
        Assert.Equal("llama-cpp-profile", llamaCppProfiles[0].Id);
        Assert.Equal("vllm-profile", vllmProfiles[0].Id);
    }

    [Fact]
    public void ProfileReader_FilterByEngines_ReturnsMultipleEngines()
    {
        // Arrange
        var reader = new ProfileReader();
        var profiles = new List<Profile>
        {
            CreateTestProfile(),  // Engine: "mlx"
            new Profile(
                SchemaVersion: 1,
                Id: "mlx-lm-profile",
                ModelHfId: "mlx-community/Llama-2-7b-chat-4bit",
                Tier: "experimental",
                Engine: "mlx-lm",
                System: new Dictionary<string, object>(),
                OMLXSettings: new Dictionary<string, object>(),
                Harness: new Dictionary<string, object>(),
                Provenance: new ProfileProvenance("author", DateTime.UtcNow.ToIso8601String(), "community"),
                Hardware: new HardwareFingerprint("Apple M2", 16, "MacBookAir")
            ),
            new Profile(
                SchemaVersion: 1,
                Id: "vllm-profile",
                ModelHfId: "meta-llama/Llama-2-70b",
                Tier: "production",
                Engine: "vllm",
                System: new Dictionary<string, object>(),
                OMLXSettings: new Dictionary<string, object>(),
                Harness: new Dictionary<string, object>(),
                Provenance: new ProfileProvenance("author", DateTime.UtcNow.ToIso8601String(), "community"),
                Hardware: new HardwareFingerprint("GPU", 80, "gpu-cluster")
            ),
        };

        // Act
        var mlxVariants = reader.FilterByEngines(profiles, "mlx", "mlx-lm");

        // Assert
        Assert.Equal(2, mlxVariants.Count);
        Assert.Contains(mlxVariants, p => p.Engine == "mlx");
        Assert.Contains(mlxVariants, p => p.Engine == "mlx-lm");
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
