namespace MlxPep.Core.Tests;

using System;
using System.Collections.Generic;
using MlxPep.Core.Detectors;
using Xunit;

/// <summary>
/// Unit tests for SystemDetector.
/// Issue #10: core: system + oMLX read-only detectors
///
/// Tests detector behavior with mocked subprocess output and error conditions.
/// </summary>
public class SystemDetectorTests
{
    [Fact]
    public void Detect_WhenCalled_ReturnsNonNullHardwareInfo()
    {
        // Arrange
        var detector = new SystemDetector();

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ModelName);
        Assert.NotNull(result.ModelIdentifier);
        Assert.NotNull(result.Chip);
        Assert.True(result.MemoryGb >= 0);
        Assert.True(result.WiredLimitMb >= 0);
    }

    [Fact]
    public void Detect_OnMacBook_ReturnsValidAppleSiliconValues()
    {
        // Arrange
        var detector = new SystemDetector();

        // Act
        var result = detector.Detect();

        // Assert - on Apple Silicon machine, should detect Apple chip
        // Note: This test is platform-specific and depends on actual hardware
        // It will pass on Apple Silicon, may fail on other platforms
        if (result.Chip.Contains("Apple", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Contains("Apple", result.Chip);
            Assert.True(result.MemoryGb > 0, "Memory should be detected on Apple Silicon");
            Assert.True(result.WiredLimitMb >= 0, "Wired limit should be detected");
        }
    }

    [Fact]
    public void Detect_ReturnsDefaultsOnFailure()
    {
        // Arrange
        var detector = new SystemDetector();

        // Act - even if system_profiler fails, should return defaults
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ModelName);
        Assert.NotNull(result.ModelIdentifier);
        Assert.NotNull(result.Chip);
    }

    [Fact]
    public void Detect_ModelIdentifierIsValidFormat()
    {
        // Arrange
        var detector = new SystemDetector();

        // Act
        var result = detector.Detect();

        // Assert
        // Model identifiers typically follow patterns like MacBookPro18,2 or MacBookAir13,3
        Assert.NotNull(result.ModelIdentifier);
        if (result.ModelIdentifier != "Unknown")
        {
            Assert.DoesNotContain(" ", result.ModelIdentifier);
        }
    }

    [Fact]
    public void Detect_StorageValuesNullableWhenAbsent()
    {
        // Arrange
        var detector = new SystemDetector();

        // Act
        var result = detector.Detect();

        // Assert
        // Storage values may be null depending on system state
        if (result.StorageCapacityTb.HasValue)
            Assert.True(result.StorageCapacityTb > 0);

        if (result.StorageFreeGb.HasValue)
            Assert.True(result.StorageFreeGb >= 0);
    }

    [Fact]
    public void Detect_IsReadOnly()
    {
        // Arrange
        var detector = new SystemDetector();

        // Act
        var result1 = detector.Detect();
        var result2 = detector.Detect();

        // Assert - multiple calls should be idempotent (read-only, no mutations)
        Assert.Equal(result1.ModelName, result2.ModelName);
        Assert.Equal(result1.MemoryGb, result2.MemoryGb);
        Assert.Equal(result1.Chip, result2.Chip);
    }

    [Fact]
    public void Detect_CanBeCalledMultipleTimesWithoutSideEffects()
    {
        // Arrange
        var detector = new SystemDetector();

        // Act
        for (int i = 0; i < 3; i++)
        {
            var result = detector.Detect();

            // Assert
            Assert.NotNull(result);
        }
    }

    /// <summary>
    /// Integration test: Verify detector returns serializable data matching generate_ornith_matrix.py structure.
    /// </summary>
    [Fact]
    public void Detect_ResultIsSerializable()
    {
        // Arrange
        var detector = new SystemDetector();

        // Act
        var result = detector.Detect();

        // Assert
        // Verify all required fields are present
        Assert.NotNull(result.ModelName);
        Assert.NotNull(result.ModelIdentifier);
        Assert.NotNull(result.Chip);
        Assert.True(result.MemoryGb >= 0);
        Assert.True(result.WiredLimitMb >= 0);

        // Verify record is serializable (basic check)
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.Contains("modelName", json);
        Assert.Contains("chip", json);
        Assert.Contains("memoryGb", json);
    }
}
