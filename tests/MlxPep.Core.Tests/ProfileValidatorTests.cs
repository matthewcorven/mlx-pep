namespace MlxPep.Core.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using MlxPep.Core;

public class ProfileValidatorTests
{
    private readonly ProfileValidator _validator = new();

    [Fact]
    public void ValidateForLocalUse_WithValidProfile_ReturnsSuccess()
    {
        // Arrange
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-high-abc123",
            ModelHfId: "test/model",
            Tier: "high",
            Engine: "mlx",
            System: new Dictionary<string, object> { { "os", "macOS" } },
            OMLXSettings: new Dictionary<string, object> { { "compute_units", "ALL" } },
            Harness: new Dictionary<string, object> { { "framework", "vscode" } },
            Provenance: new ProfileProvenance("model-assessor", DateTime.UtcNow.ToString("O"), "assess-command:workload-winner-collapse"),
            Hardware: new HardwareFingerprint("Apple M1", 16, "MacBook"),
            Sampler: null);

        // Act
        var result = _validator.ValidateForLocalUse(profile);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateForLocalUse_WithNullProfile_ReturnsFailed()
    {
        // Act
        var result = _validator.ValidateForLocalUse(null!);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("profile is required", result.Errors[0]);
    }

    [Fact]
    public void ValidateForLocalUse_WithInvalidSchemaVersion_ReturnsFailed()
    {
        // Arrange
        var profile = new Profile(
            SchemaVersion: 2,  // Invalid version
            Id: "test-high-abc123",
            ModelHfId: "test/model",
            Tier: "high",
            Engine: "mlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("model-assessor", DateTime.UtcNow.ToString("O"), "assess-command:workload-winner-collapse"),
            Hardware: new HardwareFingerprint("Test", 8, "Test"),
            Sampler: null);

        // Act
        var result = _validator.ValidateForLocalUse(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("schemaVersion must be 1", result.Errors[0]);
    }

    [Fact]
    public void ValidateForLocalUse_WithMissingId_ReturnsFailed()
    {
        // Arrange
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "",  // Invalid empty id
            ModelHfId: "test/model",
            Tier: "high",
            Engine: "mlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("model-assessor", DateTime.UtcNow.ToString("O"), "assess-command:workload-winner-collapse"),
            Hardware: new HardwareFingerprint("Test", 8, "Test"),
            Sampler: null);

        // Act
        var result = _validator.ValidateForLocalUse(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("id is required"));
    }

    [Fact]
    public void ValidateForLocalUse_WithNullModelHfId_ReturnsFailed()
    {
        // Arrange
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-high-abc123",
            ModelHfId: null!,  // Invalid null
            Tier: "high",
            Engine: "mlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("model-assessor", DateTime.UtcNow.ToString("O"), "assess-command:workload-winner-collapse"),
            Hardware: new HardwareFingerprint("Test", 8, "Test"),
            Sampler: null);

        // Act
        var result = _validator.ValidateForLocalUse(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("modelHfId is required"));
    }

    [Fact]
    public void ValidateForLocalUse_WithInvalidTier_ReturnsFailed()
    {
        // Arrange - invalid tier (must be high, balanced, or efficient)
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-unknown-abc123",
            ModelHfId: "test/model",
            Tier: "unknown",  // Invalid tier
            Engine: "mlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("model-assessor", DateTime.UtcNow.ToString("O"), "assess-command:workload-winner-collapse"),
            Hardware: new HardwareFingerprint("Test", 8, "Test"),
            Sampler: null);

        // Act
        var result = _validator.ValidateForLocalUse(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("tier must be 'high', 'balanced', or 'efficient'"));
    }

    [Fact]
    public void ValidateForLocalUse_WithNullHardware_ReturnsFailed()
    {
        // Arrange
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-high-abc123",
            ModelHfId: "test/model",
            Tier: "high",
            Engine: "mlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("model-assessor", DateTime.UtcNow.ToString("O"), "assess-command:workload-winner-collapse"),
            Hardware: null!,  // Invalid null hardware
            Sampler: null);

        // Act
        var result = _validator.ValidateForLocalUse(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("hardware is required"));
    }

    [Fact]
    public void ValidateForLocalUse_WithUnknownSystemKey_ReturnsWarning()
    {
        // Arrange - unknown system key (forward compatibility warning)
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-high-abc123",
            ModelHfId: "test/model",
            Tier: "high",
            Engine: "mlx",
            System: new Dictionary<string, object> { { "unknown_key", "value" } },  // Unknown key
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("model-assessor", DateTime.UtcNow.ToString("O"), "assess-command:workload-winner-collapse"),
            Hardware: new HardwareFingerprint("Test", 8, "Test"),
            Sampler: null);

        // Act
        var result = _validator.ValidateForLocalUse(profile);

        // Assert - should pass validation but with warning
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("Unknown key in system"));
    }

    [Fact]
    public void ValidateForLocalUse_WithSamplerSettingsOutOfRange_FailsValidation()
    {
        // Arrange - sampler with out-of-range temperature (should be 0-2, but this is 5.0)
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-high-abc123",
            ModelHfId: "test/model",
            Tier: "high",
            Engine: "mlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("model-assessor", DateTime.UtcNow.ToString("O"), "assess-command:workload-winner-collapse"),
            Hardware: new HardwareFingerprint("Test", 8, "Test"),
            Sampler: new SamplerSettings(
                Temperature: 5.0,  // Out of range! Should be 0-2
                TopP: null,
                TopK: null,
                RepetitionPenalty: null,
                ContextTokens: null));

        // Act
        var result = _validator.ValidateForLocalUse(profile);

        // Assert
        // ProfileValidator now validates sampler parameter ranges - temperature must be in [0, 2]
        Assert.False(result.IsValid);
        Assert.Contains("temperature must be in range [0, 2]", result.Errors[0]);
    }

    [Fact]
    public void ValidateForLocalUse_WithSamplerSettingsTopPOutOfRange_FailsValidation()
    {
        // Arrange - sampler with out-of-range topP (should be 0-1, but this is 1.5)
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-high-abc123",
            ModelHfId: "test/model",
            Tier: "high",
            Engine: "mlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("model-assessor", DateTime.UtcNow.ToString("O"), "assess-command:workload-winner-collapse"),
            Hardware: new HardwareFingerprint("Test", 8, "Test"),
            Sampler: new SamplerSettings(
                Temperature: null,
                TopP: 1.5,  // Out of range! Should be 0-1
                TopK: null,
                RepetitionPenalty: null,
                ContextTokens: null));

        // Act
        var result = _validator.ValidateForLocalUse(profile);

        // Assert
        // ProfileValidator now validates sampler parameter ranges - topP must be in [0, 1]
        Assert.False(result.IsValid);
        Assert.Contains("topP must be in range [0, 1]", result.Errors[0]);
    }

    [Fact]
    public void ValidateForLocalUse_WithNegativeTemperature_FailsValidation()
    {
        // Arrange - negative temperature
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-high-abc123",
            ModelHfId: "test/model",
            Tier: "high",
            Engine: "mlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("model-assessor", DateTime.UtcNow.ToString("O"), "assess-command:workload-winner-collapse"),
            Hardware: new HardwareFingerprint("Test", 8, "Test"),
            Sampler: new SamplerSettings(
                Temperature: -1.0,  // Invalid negative
                TopP: null,
                TopK: null,
                RepetitionPenalty: null,
                ContextTokens: null));

        // Act
        var result = _validator.ValidateForLocalUse(profile);

        // Assert
        // ProfileValidator now validates sampler parameter ranges - temperature must be in [0, 2]
        Assert.False(result.IsValid);
        Assert.Contains("temperature must be in range [0, 2]", result.Errors[0]);
    }

    [Fact]
    public void ValidateForLocalUse_WithNegativeTopK_FailsValidation()
    {
        // Arrange - negative topK
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-high-abc123",
            ModelHfId: "test/model",
            Tier: "high",
            Engine: "mlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("model-assessor", DateTime.UtcNow.ToString("O"), "assess-command:workload-winner-collapse"),
            Hardware: new HardwareFingerprint("Test", 8, "Test"),
            Sampler: new SamplerSettings(
                Temperature: null,
                TopP: null,
                TopK: -50,  // Invalid negative
                RepetitionPenalty: null,
                ContextTokens: null));

        // Act
        var result = _validator.ValidateForLocalUse(profile);

        // Assert
        // ProfileValidator now validates sampler parameter ranges - topK must be positive
        Assert.False(result.IsValid);
        Assert.Contains("topK must be positive", result.Errors[0]);
    }

    [Fact]
    public void ValidateForLocalUse_WithZeroContextTokens_FailsValidation()
    {
        // Arrange - zero contextTokens (invalid, should be > 0)
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-high-abc123",
            ModelHfId: "test/model",
            Tier: "high",
            Engine: "mlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("model-assessor", DateTime.UtcNow.ToString("O"), "assess-command:workload-winner-collapse"),
            Hardware: new HardwareFingerprint("Test", 8, "Test"),
            Sampler: new SamplerSettings(
                Temperature: null,
                TopP: null,
                TopK: null,
                RepetitionPenalty: null,
                ContextTokens: 0));  // Invalid zero

        // Act
        var result = _validator.ValidateForLocalUse(profile);

        // Assert
        // ProfileValidator now validates sampler parameter ranges - contextTokens must be positive
        Assert.False(result.IsValid);
        Assert.Contains("contextTokens must be positive", result.Errors[0]);
    }

    [Fact]
    public void ValidateForLocalUse_WithAllValidTiers_PassesValidation()
    {
        // Arrange & Act & Assert - test all three valid tiers
        var tiers = new[] { "high", "balanced", "efficient" };

        foreach (var tier in tiers)
        {
            var profile = new Profile(
                SchemaVersion: 1,
                Id: $"test-{tier}-abc123",
                ModelHfId: "test/model",
                Tier: tier,  // Test each valid tier
                Engine: "mlx",
                System: new Dictionary<string, object>(),
                OMLXSettings: new Dictionary<string, object>(),
                Harness: new Dictionary<string, object>(),
                Provenance: new ProfileProvenance("model-assessor", DateTime.UtcNow.ToString("O"), "assess-command:workload-winner-collapse"),
                Hardware: new HardwareFingerprint("Test", 8, "Test"),
                Sampler: null);

            var result = _validator.ValidateForLocalUse(profile);
            Assert.True(result.IsValid, $"Validation failed for tier '{tier}'");
        }
    }

    [Fact]
    public void ValidateForLocalUse_WithNullProvenance_ReturnsFailed()
    {
        // Arrange
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-high-abc123",
            ModelHfId: "test/model",
            Tier: "high",
            Engine: "mlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: null!,  // Invalid null
            Hardware: new HardwareFingerprint("Test", 8, "Test"),
            Sampler: null);

        // Act
        var result = _validator.ValidateForLocalUse(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("provenance is required"));
    }

    [Fact]
    public void ValidateForLocalUse_WithNullProvenanceAuthor_ReturnsFailed()
    {
        // Arrange
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-high-abc123",
            ModelHfId: "test/model",
            Tier: "high",
            Engine: "mlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance(null!, DateTime.UtcNow.ToString("O"), "assess-command:workload-winner-collapse"),  // Null author
            Hardware: new HardwareFingerprint("Test", 8, "Test"),
            Sampler: null);

        // Act
        var result = _validator.ValidateForLocalUse(profile);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("provenance.author is required"));
    }

    [Fact]
    public void ValidateForLocalUse_WithZeroMemoryHardware_PassesValidation()
    {
        // Arrange - hardware with zero memory (valid for minimal devices like Raspberry Pi)
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-high-abc123",
            ModelHfId: "test/model",
            Tier: "high",
            Engine: "mlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("model-assessor", DateTime.UtcNow.ToString("O"), "assess-command:workload-winner-collapse"),
            Hardware: new HardwareFingerprint("ARM64", 0, "RaspberryPi"),  // Zero memory is OK
            Sampler: null);

        // Act
        var result = _validator.ValidateForLocalUse(profile);

        // Assert
        Assert.True(result.IsValid);  // Should pass - zero memory is valid
    }

    [Fact]
    public void ValidateForLocalUse_WithUnknownOMLXKey_ReturnsWarning()
    {
        // Arrange - unknown OMLX key
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-high-abc123",
            ModelHfId: "test/model",
            Tier: "high",
            Engine: "mlx",
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object> { { "future_feature", "value" } },  // Unknown key
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("model-assessor", DateTime.UtcNow.ToString("O"), "assess-command:workload-winner-collapse"),
            Hardware: new HardwareFingerprint("Test", 8, "Test"),
            Sampler: null);

        // Act
        var result = _validator.ValidateForLocalUse(profile);

        // Assert
        Assert.True(result.IsValid);  // Should still pass
        Assert.Contains(result.Warnings, w => w.Contains("Unknown key in omlx"));
    }
}
