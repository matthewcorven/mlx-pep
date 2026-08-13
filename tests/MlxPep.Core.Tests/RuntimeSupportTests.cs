namespace MlxPep.Core.Tests;

using System;
using System.Collections.Generic;
using Xunit;

/// <summary>
/// Basic tests for runtime engine support (mlx-lm, llama.cpp, vLLM).
/// Comprehensive issue #8 tests are in Issue8ProfileSchemaTests.cs
/// Issue #25 (multi-runtime) tests and issue #27 tests are deferred.
/// </summary>
public class RuntimeEngineTests
{
    private static Profile CreateProfileForEngine(string engine, string modelId = "meta-llama/Llama-2-7b")
    {
        var engineSettings = engine switch
        {
            "omlx" => new Dictionary<string, object>
            {
                { "compute_units", "ALL" },
                { "memory_guard_tier", "balanced" }
            },
            "mlx-lm" => new Dictionary<string, object>
            {
                { "backend", "mlx-lm" },
                { "adapter_path", "" }
            },
            "llama.cpp" => new Dictionary<string, object>
            {
                { "n_gpu_layers", 40 },
                { "n_threads", 8 }
            },
            "vllm" => new Dictionary<string, object>
            {
                { "tensor_parallel_size", 1 },
                { "dtype", "float16" }
            },
            _ => new Dictionary<string, object>()
        };

        return new Profile(
            SchemaVersion: 1,
            Id: $"test-{engine}-001",
            ModelHfId: modelId,
            Tier: "balanced",
            Engine: engine,
            System: new Dictionary<string, object> { { "os", "macOS" } },
            OMLXSettings: engineSettings,
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance(
                Author: "test-author",
                CreatedAt: DateTime.UtcNow.ToString("O"),
                Source: "test"
            ),
            Hardware: new HardwareFingerprint(
                Chip: "Apple M2",
                MemoryGb: 16,
                ModelIdentifier: "MacBookPro18,2"
            ),
            Sampler: null
        );
    }

    [Theory]
    [InlineData("omlx")]
    [InlineData("mlx-lm")]
    [InlineData("llama.cpp")]
    [InlineData("vllm")]
    public void Profile_SupportedEngine_CreatesSuccessfully(string engine)
    {
        // Arrange & Act
        var profile = CreateProfileForEngine(engine);

        // Assert
        Assert.NotNull(profile);
        Assert.Equal(engine, profile.Engine);
        Assert.NotEmpty(profile.OMLXSettings);
    }

    [Fact]
    public void Profile_EngineField_StoresValue()
    {
        // Arrange
        var profile = CreateProfileForEngine("mlx-lm");

        // Act & Assert
        Assert.Equal("mlx-lm", profile.Engine);
        Assert.Contains("backend", profile.OMLXSettings.Keys);
    }

    [Fact]
    public void Profile_OMLXSettings_AreEngineSpecific()
    {
        // Arrange & Act
        var omlxProfile = CreateProfileForEngine("omlx");
        var mlxLmProfile = CreateProfileForEngine("mlx-lm");

        // Assert
        Assert.NotEqual(
            string.Join(",", omlxProfile.OMLXSettings.Keys),
            string.Join(",", mlxLmProfile.OMLXSettings.Keys)
        );
    }
}
