namespace MlxPep.Core.Tests;

using System;
using System.Text.Json;
using MlxPep.Core.Detectors;
using Xunit;

/// <summary>
/// Integration tests for detection system.
/// Issue #10: core: system + oMLX read-only detectors
///
/// Validates that both detectors work together and produce output
/// compatible with generate_ornith_matrix.py structure.
/// </summary>
public class DetectorIntegrationTests
{
    [Fact]
    public void BothDetectors_CanBeCalledTogether()
    {
        // Arrange
        var systemDetector = new SystemDetector();
        var omlxDetector = new OmlxDetector();

        // Act
        var hardware = systemDetector.Detect();
        var omlx = omlxDetector.Detect();

        // Assert
        Assert.NotNull(hardware);
        Assert.NotNull(omlx);
    }

    [Fact]
    public void CombinedResults_CanBeSerializedToJson()
    {
        // Arrange
        var systemDetector = new SystemDetector();
        var omlxDetector = new OmlxDetector();

        // Act
        var hardware = systemDetector.Detect();
        var omlx = omlxDetector.Detect();
        var combined = new DetectionResults(
            Hardware: hardware,
            Omlx: omlx,
            Timestamp: DateTime.UtcNow.ToString("O")
        );

        var json = JsonSerializer.Serialize(combined);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("hardware", json);
        Assert.Contains("omlx", json);
        Assert.Contains("timestamp", json);
    }

    [Fact]
    public void DetectionResults_MatchesPythonScriptMetadataStructure()
    {
        // Arrange
        var systemDetector = new SystemDetector();
        var omlxDetector = new OmlxDetector();

        // Act
        var hardware = systemDetector.Detect();
        var omlx = omlxDetector.Detect();

        // Assert - verify structure matches generate_ornith_matrix.py output
        // Python script emits: { "metadata": { "hardware": {...}, "omlx": {...}, "derived": {...} } }
        // Our detectors produce the hardware and omlx sections directly

        // Hardware section should contain (matching Python script):
        Assert.NotNull(hardware.ModelName);
        Assert.NotNull(hardware.ModelIdentifier);
        Assert.NotNull(hardware.Chip);
        Assert.True(hardware.MemoryGb >= 0);
        Assert.True(hardware.WiredLimitMb >= 0);

        // oMLX section should contain:
        Assert.NotNull(omlx.ConfigPath);
        Assert.NotNull(omlx.LogPath);
        Assert.NotNull(omlx.CurrentMemoryGuardTier);
    }

    [Fact]
    public void HardwareInfo_ContainsExpectedFields()
    {
        // Arrange
        var detector = new SystemDetector();

        // Act
        var hw = detector.Detect();

        // Assert - matches Python script field names and types
        Assert.NotNull(hw.ModelName);
        Assert.NotNull(hw.ModelIdentifier);
        Assert.NotNull(hw.Chip);
        Assert.IsType<int>(hw.MemoryGb);
        Assert.IsType<int>(hw.WiredLimitMb);
        // StorageFreeGb and StorageCapacityTb are nullable
    }

    [Fact]
    public void OmlxState_ContainsExpectedFields()
    {
        // Arrange
        var detector = new OmlxDetector();

        // Act
        var state = detector.Detect();

        // Assert - matches Python script field names
        Assert.NotNull(state.ConfigPath);
        Assert.NotNull(state.LogPath);
        Assert.NotNull(state.CurrentMemoryGuardTier);
        // Other fields may be null
    }

    [Fact]
    public void DetectionResults_SerializedJsonContainsAllMetadata()
    {
        // Arrange
        var systemDetector = new SystemDetector();
        var omlxDetector = new OmlxDetector();
        var hardware = systemDetector.Detect();
        var omlx = omlxDetector.Detect();
        var results = new DetectionResults(
            Hardware: hardware,
            Omlx: omlx,
            Timestamp: DateTime.UtcNow.ToString("O")
        );

        // Act
        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });

        // Assert
        Assert.NotNull(json);
        // Verify camelCase field names (as per C# record JsonPropertyName attributes)
        Assert.Contains("\"hardware\"", json);
        Assert.Contains("\"omlx\"", json);
        Assert.Contains("\"modelName\"", json);
        Assert.Contains("\"chip\"", json);
        Assert.Contains("\"currentMemoryGuardTier\"", json);
    }

    [Fact]
    public void DetectionResults_CanBeDeserializedFromJson()
    {
        // Arrange
        var systemDetector = new SystemDetector();
        var omlxDetector = new OmlxDetector();
        var original = new DetectionResults(
            Hardware: systemDetector.Detect(),
            Omlx: omlxDetector.Detect(),
            Timestamp: DateTime.UtcNow.ToString("O")
        );
        var json = JsonSerializer.Serialize(original);

        // Act
        var deserialized = JsonSerializer.Deserialize<DetectionResults>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.Hardware);
        Assert.NotNull(deserialized.Omlx);
        Assert.Equal(original.Hardware.Chip, deserialized.Hardware.Chip);
        Assert.Equal(original.Hardware.MemoryGb, deserialized.Hardware.MemoryGb);
        Assert.Equal(original.Omlx.CurrentMemoryGuardTier, deserialized.Omlx.CurrentMemoryGuardTier);
    }

    /// <summary>
    /// Validates that detector output is compatible with the MlxProfile schema.
    /// Issue #8 defined the profile schema; detectors should produce hardware metadata
    /// that can populate a Profile's Hardware field.
    /// </summary>
    [Fact]
    public void DetectedHardwareInfo_IsCompatibleWithProfileHardwareFingerprint()
    {
        // Arrange
        var detector = new SystemDetector();
        var detected = detector.Detect();

        // Act - create a HardwareFingerprint from detected values
        var fingerprint = new HardwareFingerprint(
            Chip: detected.Chip,
            MemoryGb: detected.MemoryGb,
            ModelIdentifier: detected.ModelIdentifier
        );

        // Assert
        Assert.NotNull(fingerprint);
        Assert.Equal(detected.Chip, fingerprint.Chip);
        Assert.Equal(detected.MemoryGb, fingerprint.MemoryGb);
        Assert.Equal(detected.ModelIdentifier, fingerprint.ModelIdentifier);
    }

    [Fact]
    public void AllDetectors_ExecuteWithoutThrowingExceptions()
    {
        // Arrange
        var systemDetector = new SystemDetector();
        var omlxDetector = new OmlxDetector();

        // Act & Assert - should not throw
        var hw = systemDetector.Detect();
        var omlx = omlxDetector.Detect();
        var results = new DetectionResults(hw, omlx, DateTime.UtcNow.ToString("O"));

        Assert.NotNull(hw);
        Assert.NotNull(omlx);
        Assert.NotNull(results);
    }
}
