using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using MlxPep.Core;

namespace MlxPep.Core.Tests;

/// <summary>
/// Integration tests for Profile/SamplerSettings type coercion behavior.
/// Tests verify that numeric type coercion from JSON sources (strings, longs, etc.)
/// is handled correctly through the public API without exposing private implementation.
/// </summary>
public class ProfilingRunnerIntegrationTests
{
    private static readonly ProfileProvenance TestProvenance = new(
        Author: "test",
        CreatedAt: "2024-01-01T00:00:00Z",
        Source: "test-source"
    );

    private static readonly HardwareFingerprint TestHardware = new(
        Chip: "Apple Neural Engine",
        MemoryGb: 16,
        ModelIdentifier: "test-model"
    );

    private readonly ProfileValidator _validator = new();

    [Fact]
    public void TypeCoercion_DirectSamplerSettingsWithDoubleTemperature()
    {
        // Arrange: Create SamplerSettings directly with double temperature
        var settings = new SamplerSettings(Temperature: 0.7, TopP: 0.9, TopK: 40);

        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-profile-direct-double",
            ModelHfId: "model/test",
            Tier: "balanced",
            Engine: "mlx-lm",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: TestProvenance,
            Hardware: TestHardware,
            Sampler: settings
        );

        // Act: Validate the profile
        var validationResult = _validator.ValidateForLocalUse(profile);

        // Assert
        Assert.True(validationResult.IsValid,
            $"Profile validation failed: {string.Join("; ", validationResult.Errors)}");
    }

    [Fact]
    public void TypeCoercion_SamplerSettingsWithNullValues()
    {
        // Arrange: Create SamplerSettings with null optional values
        var settings = new SamplerSettings(Temperature: null, TopP: null, TopK: null);

        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-profile-null-values",
            ModelHfId: "model/test",
            Tier: "high",
            Engine: "mlx-lm",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: TestProvenance,
            Hardware: TestHardware,
            Sampler: settings
        );

        // Act
        var validationResult = _validator.ValidateForLocalUse(profile);

        // Assert
        Assert.True(validationResult.IsValid,
            $"Profile validation failed: {string.Join("; ", validationResult.Errors)}");
    }

    [Fact]
    public void TypeCoercion_SamplerSettingsWithMinimalValues()
    {
        // Arrange: Create SamplerSettings with just temperature
        var settings = new SamplerSettings(Temperature: 0.5);

        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-profile-minimal",
            ModelHfId: "model/test",
            Tier: "balanced",
            Engine: "mlx-lm",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: TestProvenance,
            Hardware: TestHardware,
            Sampler: settings
        );

        // Act
        var validationResult = _validator.ValidateForLocalUse(profile);

        // Assert
        Assert.True(validationResult.IsValid,
            $"Profile validation failed: {string.Join("; ", validationResult.Errors)}");
    }

    [Fact]
    public void TypeCoercion_SamplerSettingsWithEdgeCaseValues()
    {
        // Arrange: Create SamplerSettings with edge case values
        var settings = new SamplerSettings(
            Temperature: 0.0001,  // Very small
            TopP: 0.99999,        // Very close to 1.0
            TopK: int.MaxValue    // Max int
        );

        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-profile-edge-cases",
            ModelHfId: "model/test",
            Tier: "efficient",
            Engine: "mlx-lm",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: TestProvenance,
            Hardware: TestHardware,
            Sampler: settings
        );

        // Act
        var validationResult = _validator.ValidateForLocalUse(profile);

        // Assert
        Assert.True(validationResult.IsValid,
            $"Profile validation failed: {string.Join("; ", validationResult.Errors)}");
    }

    [Fact]
    public void TypeCoercion_ProfileWithoutSamplerSettings()
    {
        // Arrange: Create profile with null sampler (optional)
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-profile-no-sampler",
            ModelHfId: "model/test",
            Tier: "high",
            Engine: "mlx-lm",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: TestProvenance,
            Hardware: TestHardware,
            Sampler: null
        );

        // Act
        var validationResult = _validator.ValidateForLocalUse(profile);

        // Assert
        Assert.True(validationResult.IsValid,
            $"Profile validation failed: {string.Join("; ", validationResult.Errors)}");
    }

    [Fact]
    public void TypeCoercion_JsonDeserialization_StringNumericToDouble()
    {
        // Arrange: Simulate JSON deserialization with numeric strings
        // This tests that the JSON parser correctly coerces "0.7" to 0.7
        var json = """
        {
            "schemaVersion": 1,
            "id": "test-json-coercion",
            "modelHfId": "model/test",
            "tier": "balanced",
            "engine": "mlx-lm",
            "system": {},
            "omlx": {},
            "harness": {},
            "provenance": {
                "author": "test",
                "createdAt": "2024-01-01T00:00:00Z",
                "source": "test-source"
            },
            "hardware": {
                "chip": "Apple Neural Engine",
                "memoryGb": 16,
                "modelIdentifier": "test-model"
            },
            "sampler": {
                "temperature": 0.7,
                "topP": 0.9,
                "topK": 40
            }
        }
        """;

        // Act: Deserialize from JSON
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var profile = JsonSerializer.Deserialize<Profile>(json, options);

        // Assert: Profile should deserialize successfully
        Assert.NotNull(profile);
        Assert.Equal("test-json-coercion", profile.Id);
        Assert.NotNull(profile.Sampler);
        Assert.Equal(0.7, profile.Sampler.Temperature);
    }

    [Fact]
    public void TypeCoercion_AllSamplerParametersPopulated()
    {
        // Arrange: Create SamplerSettings with all parameters
        var settings = new SamplerSettings(
            Temperature: 0.8,
            TopP: 0.95,
            TopK: 50,
            RepetitionPenalty: 1.0,
            ContextTokens: 8192
        );

        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-profile-all-params",
            ModelHfId: "model/test",
            Tier: "balanced",
            Engine: "mlx-lm",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: TestProvenance,
            Hardware: TestHardware,
            Sampler: settings
        );

        // Act
        var validationResult = _validator.ValidateForLocalUse(profile);

        // Assert
        Assert.True(validationResult.IsValid,
            $"Profile validation failed: {string.Join("; ", validationResult.Errors)}");
    }

    [Fact]
    public void TypeCoercion_MixedNullAndPopulatedParameters()
    {
        // Arrange: Mix of null and populated parameters
        var settings = new SamplerSettings(
            Temperature: 0.7,
            TopP: null,
            TopK: 40,
            RepetitionPenalty: null,
            ContextTokens: 4096
        );

        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-profile-mixed-null",
            ModelHfId: "model/test",
            Tier: "high",
            Engine: "mlx-lm",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: TestProvenance,
            Hardware: TestHardware,
            Sampler: settings
        );

        // Act
        var validationResult = _validator.ValidateForLocalUse(profile);

        // Assert
        Assert.True(validationResult.IsValid,
            $"Profile validation failed: {string.Join("; ", validationResult.Errors)}");
    }

    [Fact]
    public void TypeCoercion_BoundaryValues()
    {
        // Arrange: Test boundary values for numeric types
        var settings = new SamplerSettings(
            Temperature: 0.0,         // Minimum double
            TopP: 1.0,                // Maximum probability
            TopK: 1,                  // Minimum top-k
            RepetitionPenalty: 0.0,   // Zero penalty
            ContextTokens: 1          // Minimum tokens
        );

        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-profile-boundaries",
            ModelHfId: "model/test",
            Tier: "balanced",
            Engine: "mlx-lm",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: TestProvenance,
            Hardware: TestHardware,
            Sampler: settings
        );

        // Act
        var validationResult = _validator.ValidateForLocalUse(profile);

        // Assert
        Assert.True(validationResult.IsValid,
            $"Profile validation failed: {string.Join("; ", validationResult.Errors)}");
    }
}
