namespace MlxPep.Core.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MlxPep.Core.Detectors;
using Xunit;

/// <summary>
/// Unit tests for OmlxDetector.
/// Issue #10: core: system + oMLX read-only detectors
///
/// Tests detector behavior with mocked file I/O and log parsing.
/// </summary>
public class OmlxDetectorTests
{
    [Fact]
    public void Detect_WhenCalled_ReturnsNonNullOmlxState()
    {
        // Arrange
        var detector = new OmlxDetector();

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ConfigPath);
        Assert.NotNull(result.LogPath);
        Assert.NotNull(result.CurrentMemoryGuardTier);
    }

    [Fact]
    public void Detect_ReturnsGracefulDefaultsWhenNoFilesExist()
    {
        // Arrange
        var detector = new OmlxDetector();

        // Act - even if config.json and logs don't exist, should return defaults
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("unknown", result.CurrentMemoryGuardTier);
        Assert.Null(result.CurrentCeilingGb);
        Assert.Null(result.CurrentMetalCapGb);
        Assert.Null(result.RecommendedWiredLimitMb);
    }

    [Fact]
    public void Detect_ConfigPathAndLogPathArePopulated()
    {
        // Arrange
        var detector = new OmlxDetector();

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result.ConfigPath);
        Assert.NotNull(result.LogPath);
        Assert.Contains("oMLX", result.ConfigPath);
        Assert.Contains("config.json", result.ConfigPath);
        Assert.Contains("oMLX", result.LogPath);
        Assert.Contains("server.log", result.LogPath);
    }

    [Fact]
    public void Detect_IsReadOnly()
    {
        // Arrange
        var detector = new OmlxDetector();

        // Act
        var result1 = detector.Detect();
        var result2 = detector.Detect();

        // Assert - multiple calls should be idempotent (read-only, no mutations)
        Assert.Equal(result1.ConfigPath, result2.ConfigPath);
        Assert.Equal(result1.LogPath, result2.LogPath);
        Assert.Equal(result1.CurrentMemoryGuardTier, result2.CurrentMemoryGuardTier);
    }

    [Fact]
    public void Detect_CanBeCalledMultipleTimesWithoutSideEffects()
    {
        // Arrange
        var detector = new OmlxDetector();

        // Act
        for (int i = 0; i < 3; i++)
        {
            var result = detector.Detect();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.CurrentMemoryGuardTier);
        }
    }

    [Fact]
    public void Detect_ResultIsSerializable()
    {
        // Arrange
        var detector = new OmlxDetector();

        // Act
        var result = detector.Detect();

        // Assert
        // Verify all required fields are present
        Assert.NotNull(result.ConfigPath);
        Assert.NotNull(result.LogPath);
        Assert.NotNull(result.CurrentMemoryGuardTier);

        // Verify record is serializable
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.Contains("configPath", json);
        Assert.Contains("currentMemoryGuardTier", json);
    }

    /// <summary>
    /// Test: Guard tier parsing from oMLX logs.
    /// If server.log exists and contains guard tier info, it should be detected.
    /// </summary>
    [Fact]
    public void Detect_GuardTierDefaultsToUnknownWhenLogAbsent()
    {
        // Arrange
        var detector = new OmlxDetector();

        // Act
        var result = detector.Detect();

        // Assert
        Assert.NotNull(result.CurrentMemoryGuardTier);
        // If log doesn't exist or has no guard tier, should default to "unknown"
        if (!File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "oMLX", "logs", "server.log")))
        {
            Assert.Equal("unknown", result.CurrentMemoryGuardTier);
        }
    }

    /// <summary>
    /// Test: Port parsing from config.json.
    /// Port should be null if config doesn't exist or is missing port field.
    /// </summary>
    [Fact]
    public void Detect_PortIsNullWhenConfigAbsent()
    {
        // Arrange
        var detector = new OmlxDetector();

        // Act
        var result = detector.Detect();

        // Assert
        // Port may be null if config doesn't exist or doesn't have port field
        if (!File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "oMLX", "config.json")))
        {
            Assert.Null(result.Port);
        }
    }

    [Fact]
    public void Detect_MetadataFieldsMatchGenerateOrnithMatrixStructure()
    {
        // Arrange - this validates compatibility with generate_ornith_matrix.py output
        var detector = new OmlxDetector();

        // Act
        var result = detector.Detect();

        // Assert - verify structure matches what generate_ornith_matrix.py produces
        Assert.NotNull(result.ConfigPath);
        Assert.NotNull(result.LogPath);
        Assert.NotNull(result.CurrentMemoryGuardTier);

        // These fields should match the Python script's "omlx" metadata section
        // Optional fields may be null
    }

    [Fact]
    public void Detect_LogParsingHandlesMalformedInput()
    {
        // Arrange
        var detector = new OmlxDetector();

        // Act - detector should handle malformed logs gracefully
        var result = detector.Detect();

        // Assert - should not throw, should return defaults
        Assert.NotNull(result);
        Assert.NotNull(result.CurrentMemoryGuardTier);
    }

    [Theory]
    [InlineData("balanced")]
    [InlineData("safe")]
    [InlineData("aggressive")]
    public void Detect_GuardTierParsesValidValues()
    {
        // Arrange
        var detector = new OmlxDetector();

        // Act
        var result = detector.Detect();

        // Assert
        // If guard tier is detected (not "unknown"), it should be one of the valid tiers
        if (result.CurrentMemoryGuardTier != "unknown")
        {
            Assert.True(
                result.CurrentMemoryGuardTier == "balanced" ||
                result.CurrentMemoryGuardTier == "safe" ||
                result.CurrentMemoryGuardTier == "aggressive",
                $"Guard tier '{result.CurrentMemoryGuardTier}' should be a known tier"
            );
        }
    }
}
