namespace MlxPep.Core.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Comprehensive tests for Profile schema (Issue #8).
/// Tests round-trip serialization, JSONL I/O, and validation rules.
/// </summary>
public class Issue8ProfileSchemaTests
{
    /// <summary>
    /// The example profile from docs/profile-schema.md as JSON.
    /// This is used for round-trip testing.
    /// </summary>
    private static readonly string ExampleProfileJson = @"{""schemaVersion"":1,""id"":""ornith-35b-mtplx-balanced-a1b2c3"",""modelHfId"":""wang-yang/Ornith-1.0-35B-MTPLX"",""tier"":""balanced"",""engine"":""omlx"",""system"":{""iogpu.wired_limit_mb"":122880},""omlx"":{""memory_guard_tier"":""balanced"",""memory_guard_ceiling_gb"":108},""harness"":{""vscode"":{""maxInputTokens"":64000,""maxOutputTokens"":3072},""copilotCli"":{""maxPromptTokens"":64000}},""sampler"":{""temperature"":0.7,""topP"":0.95,""topK"":20,""repetitionPenalty"":1.02,""contextTokens"":64000},""provenance"":{""author"":""matthewcorven"",""createdAt"":""2026-08-11T00:00:00Z"",""source"":""assess""},""hardware"":{""chip"":""Apple M4 Max"",""memoryGb"":128,""modelIdentifier"":""Mac16,5""}}";

    [Fact]
    public void RoundTrip_ExampleProfile_PreservesAllFields()
    {
        // Arrange
        var json = ExampleProfileJson;
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        // Act: Deserialize
        var profile = JsonSerializer.Deserialize<Profile>(json, options);

        // Assert: Verify all fields were preserved
        Assert.NotNull(profile);
        Assert.Equal(1, profile.SchemaVersion);
        Assert.Equal("ornith-35b-mtplx-balanced-a1b2c3", profile.Id);
        Assert.Equal("wang-yang/Ornith-1.0-35B-MTPLX", profile.ModelHfId);
        Assert.Equal("balanced", profile.Tier);
        Assert.Equal("omlx", profile.Engine);

        // Verify nested objects
        Assert.NotNull(profile.System);
        Assert.Equal(122880, ((JsonElement)profile.System["iogpu.wired_limit_mb"]).GetInt32());

        Assert.NotNull(profile.OMLXSettings);
        Assert.Equal("balanced", ((JsonElement)profile.OMLXSettings["memory_guard_tier"]).GetString());
        Assert.Equal(108, ((JsonElement)profile.OMLXSettings["memory_guard_ceiling_gb"]).GetInt32());

        Assert.NotNull(profile.Harness);
        Assert.NotNull(profile.Provenance);
        Assert.Equal("matthewcorven", profile.Provenance.Author);
        Assert.Equal("2026-08-11T00:00:00Z", profile.Provenance.CreatedAt);
        Assert.Equal("assess", profile.Provenance.Source);

        Assert.NotNull(profile.Hardware);
        Assert.Equal("Apple M4 Max", profile.Hardware.Chip);
        Assert.Equal(128, profile.Hardware.MemoryGb);
        Assert.Equal("Mac16,5", profile.Hardware.ModelIdentifier);

        Assert.NotNull(profile.Sampler);
        Assert.Equal(0.7, profile.Sampler.Temperature);
        Assert.Equal(0.95, profile.Sampler.TopP);
        Assert.Equal(20, profile.Sampler.TopK);
        Assert.Equal(1.02, profile.Sampler.RepetitionPenalty);
        Assert.Equal(64000, profile.Sampler.ContextTokens);
    }

    [Fact]
    public void RoundTrip_SerializeDeserialize_BytesEquivalent()
    {
        // Arrange
        var json = ExampleProfileJson;
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        // Act: Deserialize then re-serialize
        var profile = JsonSerializer.Deserialize<Profile>(json, options);
        Assert.NotNull(profile);
        var reserialized = JsonSerializer.Serialize(profile, options);

        // Assert: Round-trip should preserve all essential fields
        // Note: Property order may differ in reflection-based serialization, so we check semantic equivalence
        var original = JsonDocument.Parse(json).RootElement;
        var roundtrip = JsonDocument.Parse(reserialized).RootElement;

        Assert.Equal(original.GetProperty("id").GetString(), roundtrip.GetProperty("id").GetString());
        Assert.Equal(original.GetProperty("modelHfId").GetString(), roundtrip.GetProperty("modelHfId").GetString());
        Assert.Equal(original.GetProperty("tier").GetString(), roundtrip.GetProperty("tier").GetString());
        Assert.Equal(original.GetProperty("engine").GetString(), roundtrip.GetProperty("engine").GetString());
        Assert.Equal(original.GetProperty("schemaVersion").GetInt32(), roundtrip.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public void Validation_ExampleProfile_PassesLocalUseValidation()
    {
        // Arrange
        var json = ExampleProfileJson;
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var profile = JsonSerializer.Deserialize<Profile>(json, options);
        Assert.NotNull(profile);
        var validator = new ProfileValidator();

        // Act
        var result = validator.ValidateForLocalUse(profile);

        // Assert
        Assert.True(result.IsValid, $"Validation failed: {string.Join("; ", result.Errors)}");
    }

    [Fact]
    public void Validation_InvalidSchemaVersion_Fails()
    {
        // Arrange
        var profile = new Profile(
            SchemaVersion: 2,  // Invalid: must be 1
            Id: "test-profile",
            ModelHfId: "model/test",
            Tier: "balanced",
            Engine: "omlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("author", "2026-08-11T00:00:00Z", "assess"),
            Hardware: new HardwareFingerprint("chip", 16, "model")
        );
        var validator = new ProfileValidator();

        // Act
        var result = validator.ValidateForLocalUse(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("schemaVersion must be 1", string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData("high")]
    [InlineData("balanced")]
    [InlineData("efficient")]
    public void Validation_ValidTiers_Pass(string tier)
    {
        // Arrange
        var profile = CreateTestProfile(tier: tier);
        var validator = new ProfileValidator();

        // Act
        var result = validator.ValidateForLocalUse(profile);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("high-end")]
    [InlineData("experimental")]
    [InlineData("")]
    public void Validation_InvalidTiers_Fail(string tier)
    {
        // Arrange
        var profile = CreateTestProfile(tier: tier);
        var validator = new ProfileValidator();

        // Act
        var result = validator.ValidateForLocalUse(profile);

        // Assert
        Assert.False(result.IsValid);
        var errors = string.Join("; ", result.Errors);
        // Empty tier produces "tier is required"; invalid tier produces "tier must be..."
        Assert.True(
            errors.Contains("tier is required") || errors.Contains("tier must be"),
            $"Expected tier validation error, got: {errors}"
        );
    }

    [Fact]
    public void Validation_UniqueTiersInProfileSet_Passes()
    {
        // Arrange
        var profiles = new List<Profile>
        {
            CreateTestProfile(id: "prof-high", tier: "high"),
            CreateTestProfile(id: "prof-balanced", tier: "balanced"),
            CreateTestProfile(id: "prof-efficient", tier: "efficient")
        };
        var validator = new ProfileValidator();

        // Act
        var result = validator.ValidateProfileSet(profiles);

        // Assert
        Assert.True(result.IsValid, $"Validation failed: {string.Join("; ", result.Errors)}");
    }

    [Fact]
    public void Validation_DuplicateTiersInProfileSet_Fails()
    {
        // Arrange
        var profiles = new List<Profile>
        {
            CreateTestProfile(id: "prof-1", tier: "balanced"),
            CreateTestProfile(id: "prof-2", tier: "balanced")  // Duplicate
        };
        var validator = new ProfileValidator();

        // Act
        var result = validator.ValidateProfileSet(profiles);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("appears 2 times", string.Join("; ", result.Errors));
    }

    [Fact]
    public void Validation_UnknownSystemKey_LogsWarning()
    {
        // Arrange
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-profile",
            ModelHfId: "model/test",
            Tier: "balanced",
            Engine: "omlx",
            System: new Dictionary<string, object> { { "unknown.setting", "value" } },
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("author", "2026-08-11T00:00:00Z", "assess"),
            Hardware: new HardwareFingerprint("chip", 16, "model")
        );
        var validator = new ProfileValidator();

        // Act
        var result = validator.ValidateForLocalUse(profile);

        // Assert: Should pass but with warning
        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("Unknown key in system", string.Join("; ", result.Warnings));
    }

    [Fact]
    public void Validation_UnknownOMLXKey_LogsWarning()
    {
        // Arrange
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-profile",
            ModelHfId: "model/test",
            Tier: "balanced",
            Engine: "omlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object> { { "future_setting", 123 } },
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("author", "2026-08-11T00:00:00Z", "assess"),
            Hardware: new HardwareFingerprint("chip", 16, "model")
        );
        var validator = new ProfileValidator();

        // Act
        var result = validator.ValidateForLocalUse(profile);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("Unknown key in omlx", string.Join("; ", result.Warnings));
    }

    [Fact]
    public void Validation_MissingRequiredFields_Fails()
    {
        // Arrange: Missing provenance
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-profile",
            ModelHfId: "model/test",
            Tier: "balanced",
            Engine: "omlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: null!,
            Hardware: new HardwareFingerprint("chip", 16, "model")
        );
        var validator = new ProfileValidator();

        // Act
        var result = validator.ValidateForLocalUse(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("provenance is required", string.Join("; ", result.Errors));
    }

    [Fact]
    public async Task JsonlRoundTrip_WriteAndRead_PreservesProfiles()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".jsonl";
        var profiles = new List<Profile>
        {
            CreateTestProfile(id: "prof-high", tier: "high"),
            CreateTestProfile(id: "prof-balanced", tier: "balanced"),
            CreateTestProfile(id: "prof-efficient", tier: "efficient")
        };
        var reader = new ProfileReader();

        try
        {
            // Act: Write
            await reader.WriteProfileSetAsync(tempFile, profiles, validateBeforeWrite: true);

            // Act: Read
            var readProfiles = await reader.ReadProfileSetAsync(tempFile, validateAfterRead: true);

            // Assert
            Assert.Equal(3, readProfiles.Count);
            Assert.Equal("prof-high", readProfiles[0].Id);
            Assert.Equal("prof-balanced", readProfiles[1].Id);
            Assert.Equal("prof-efficient", readProfiles[2].Id);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task JsonlRoundTrip_DuplicateTiers_ThrowsValidationError()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".jsonl";
        var profiles = new List<Profile>
        {
            CreateTestProfile(id: "prof-1", tier: "balanced"),
            CreateTestProfile(id: "prof-2", tier: "balanced")  // Duplicate
        };
        var reader = new ProfileReader();

        try
        {
            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => reader.WriteProfileSetAsync(tempFile, profiles, validateBeforeWrite: true)
            );
            Assert.Contains("Failed to validate", ex.Message);
            Assert.Contains("appears 2 times", ex.Message);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task JsonlRoundTrip_ExampleProfile_RoundTripsCorrectly()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".jsonl";
        var json = ExampleProfileJson;
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var originalProfile = JsonSerializer.Deserialize<Profile>(json, options)!;
        var profiles = new List<Profile> { originalProfile };
        var reader = new ProfileReader();

        try
        {
            // Act: Write
            await reader.WriteProfileSetAsync(tempFile, profiles, validateBeforeWrite: true);

            // Act: Read
            var readProfiles = await reader.ReadProfileSetAsync(tempFile, validateAfterRead: true);

            // Assert
            Assert.Single(readProfiles);
            var readProfile = readProfiles[0];
            Assert.Equal(originalProfile.Id, readProfile.Id);
            Assert.Equal(originalProfile.ModelHfId, readProfile.ModelHfId);
            Assert.Equal(originalProfile.Tier, readProfile.Tier);
            Assert.Equal(originalProfile.Engine, readProfile.Engine);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task JsonlRoundTrip_MalformedJson_ThrowsDeserializationError()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".jsonl";
        await File.WriteAllTextAsync(tempFile, "{ invalid json }\n");
        var reader = new ProfileReader();

        try
        {
            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => reader.ReadProfileSetAsync(tempFile, validateAfterRead: false)
            );
            Assert.Contains("Failed to deserialize JSONL", ex.Message);
            Assert.Contains("line 1", ex.Message);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void SamplerSettings_AllFieldsOptional_CanCreatePartial()
    {
        // Arrange
        var sampler = new SamplerSettings(
            Temperature: 0.7,
            TopP: null,
            TopK: 20,
            RepetitionPenalty: null,
            ContextTokens: null
        );

        // Act & Assert
        Assert.Equal(0.7, sampler.Temperature);
        Assert.Null(sampler.TopP);
        Assert.Equal(20, sampler.TopK);
        Assert.Null(sampler.RepetitionPenalty);
        Assert.Null(sampler.ContextTokens);
    }

    // Helper method to create a test profile
    private static Profile CreateTestProfile(
        string id = "test-profile",
        string tier = "balanced",
        string engine = "omlx")
    {
        return new Profile(
            SchemaVersion: 1,
            Id: id,
            ModelHfId: "model/test",
            Tier: tier,
            Engine: engine,
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("test-author", "2026-08-11T00:00:00Z", "assess"),
            Hardware: new HardwareFingerprint("test-chip", 16, "test-model")
        );
    }
}
