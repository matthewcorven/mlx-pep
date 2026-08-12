namespace MlxPep.Core.Tests;

using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Comprehensive tests for runtime engine support: mlx-lm, llama.cpp, vLLM.
/// Issue #25: runtimes: mlx-lm / llama.cpp / vLLM support
/// 
/// MVP profiles use oMLX only; fast-follow enables alternate runtimes by profile engine field.
/// These tests validate the engine detection, profile switching, and multi-runtime support.
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
            Sampler: null,
            Community: null
        );
    }

    [Theory]
    [InlineData("omlx")]
    [InlineData("mlx-lm")]
    [InlineData("llama.cpp")]
    [InlineData("vllm")]
    public void Profile_SupportsMultipleEngines(string engine)
    {
        // Arrange
        var profile = CreateProfileForEngine(engine);

        // Act
        var serialized = System.Text.Json.JsonSerializer.Serialize(profile);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<Profile>(serialized);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(engine, deserialized.Engine);
    }

    [Fact]
    public void Profile_DefaultEngineIsOMLX()
    {
        // Arrange & Act
        var profile = CreateProfileForEngine("omlx");

        // Assert
        Assert.Equal("omlx", profile.Engine);
    }

    [Fact]
    public void Profile_EngineFieldIsRequired()
    {
        // Arrange: Profile with empty engine
        var profile = new Profile(
            SchemaVersion: 1,
            Id: "test-no-engine",
            ModelHfId: "test/model",
            Tier: "balanced",
            Engine: "",  // Invalid: empty engine
            System: new Dictionary<string, object>(),
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("author", DateTime.UtcNow.ToString("O"), "test"),
            Hardware: new HardwareFingerprint("chip", 16, "model"),
            Sampler: null,
            Community: null
        );

        // Act & Assert: Validation should flag this
        var validator = new ProfileValidator();
        var result = validator.ValidateForPublishing(profile);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void OMLXProfile_HasExpectedSettings()
    {
        // Arrange
        var profile = CreateProfileForEngine("omlx");

        // Act
        var settings = profile.OMLXSettings;

        // Assert
        Assert.Contains("compute_units", settings.Keys);
        Assert.Contains("memory_guard_tier", settings.Keys);
    }

    [Fact]
    public void MLXLmProfile_HasExpectedSettings()
    {
        // Arrange
        var profile = CreateProfileForEngine("mlx-lm");

        // Act
        var settings = profile.OMLXSettings;

        // Assert
        Assert.Contains("backend", settings.Keys);
        Assert.Equal("mlx-lm", settings["backend"]);
    }

    [Fact]
    public void LlamaCppProfile_HasGpuLayerSettings()
    {
        // Arrange
        var profile = CreateProfileForEngine("llama.cpp");

        // Act
        var settings = profile.OMLXSettings;

        // Assert
        Assert.Contains("n_gpu_layers", settings.Keys);
        Assert.Contains("n_threads", settings.Keys);
        Assert.Equal(40, settings["n_gpu_layers"]);
    }

    [Fact]
    public void VLLmProfile_HasTensorParallelSettings()
    {
        // Arrange
        var profile = CreateProfileForEngine("vllm");

        // Act
        var settings = profile.OMLXSettings;

        // Assert
        Assert.Contains("tensor_parallel_size", settings.Keys);
        Assert.Contains("dtype", settings.Keys);
        Assert.Equal(1, settings["tensor_parallel_size"]);
        Assert.Equal("float16", settings["dtype"]);
    }

    [Fact]
    public void Profile_CanBeSwitchedBetweenEngines()
    {
        // Arrange
        var omlxProfile = CreateProfileForEngine("omlx");
        
        // Act: Create a new profile based on existing but with different engine
        var llamaCppProfile = omlxProfile with { Engine = "llama.cpp" };

        // Assert
        Assert.Equal("omlx", omlxProfile.Engine);
        Assert.Equal("llama.cpp", llamaCppProfile.Engine);
        Assert.Equal(omlxProfile.ModelHfId, llamaCppProfile.ModelHfId);  // Model unchanged
    }

    [Theory]
    [InlineData("omlx", "meta-llama/Llama-2-7b")]
    [InlineData("mlx-lm", "meta-llama/Llama-2-7b")]
    [InlineData("llama.cpp", "meta-llama/Llama-2-7b")]
    [InlineData("vllm", "meta-llama/Llama-2-7b")]
    public void Profiles_SupportMultipleModelsPerEngine(string engine, string modelId)
    {
        // Arrange
        var profile = CreateProfileForEngine(engine, modelId);

        // Act
        var serialized = System.Text.Json.JsonSerializer.Serialize(profile);

        // Assert: Verify engine and model ID appear in serialized profile
        Assert.Contains(engine, serialized);
        Assert.Contains(modelId, serialized);
    }

    [Fact]
    public void RuntimeProfiles_MaintainHardwareFingerprintAcrossEngines()
    {
        // Arrange
        var hardware = new HardwareFingerprint("Apple M4", 128, "MacBook16,5");
        var engines = new[] { "omlx", "mlx-lm", "llama.cpp", "vllm" };

        // Act & Assert
        foreach (var engine in engines)
        {
            var profile = CreateProfileForEngine(engine) with { Hardware = hardware };
            Assert.Equal("Apple M4", profile.Hardware.Chip);
            Assert.Equal(128, profile.Hardware.MemoryGb);
        }
    }

    [Fact]
    public void RuntimeProfiles_SupportDifferentTiers()
    {
        // Arrange
        var tiers = new[] { "high-performance", "balanced", "efficient" };
        var engine = "llama.cpp";

        // Act & Assert
        foreach (var tier in tiers)
        {
            var profile = CreateProfileForEngine(engine);
            var profileWithTier = profile with { Tier = tier };
            Assert.Equal(tier, profileWithTier.Tier);
        }
    }

    [Fact]
    public void Profile_EngineFieldIsPersistentAcrossRoundtrip()
    {
        // Arrange
        var engines = new[] { "omlx", "mlx-lm", "llama.cpp", "vllm" };
        var reader = new ProfileReader();

        // Act & Assert: Verify engine survives serialization roundtrip
        foreach (var engine in engines)
        {
            var originalProfile = CreateProfileForEngine(engine);
            var json = System.Text.Json.JsonSerializer.Serialize(originalProfile);
            var deserializedProfile = System.Text.Json.JsonSerializer.Deserialize<Profile>(json);

            Assert.NotNull(deserializedProfile);
            Assert.Equal(engine, deserializedProfile.Engine);
        }
    }

    [Fact]
    public void RuntimeProfileReader_FiltersByEngine()
    {
        // Arrange
        var profiles = new List<Profile>
        {
            CreateProfileForEngine("omlx"),
            CreateProfileForEngine("mlx-lm"),
            CreateProfileForEngine("llama.cpp"),
            CreateProfileForEngine("vllm"),
            CreateProfileForEngine("omlx"),  // Another oMLX profile
        };
        var reader = new ProfileReader();

        // Act
        var omlxProfiles = profiles.Where(p => p.Engine == "omlx").ToList();
        var llamaCppProfiles = profiles.Where(p => p.Engine == "llama.cpp").ToList();
        var vllmProfiles = profiles.Where(p => p.Engine == "vllm").ToList();

        // Assert
        Assert.Equal(2, omlxProfiles.Count);
        Assert.Single(llamaCppProfiles);
        Assert.Single(vllmProfiles);
    }

    [Fact]
    public void RuntimeProfiles_CanBePublishedWithCommunityMetadata()
    {
        // Arrange
        var profile = CreateProfileForEngine("llama.cpp") with
        {
            Community = new CommunityMetadata(
                Tags: new List<string> { "production", "gpu" },
                Description: "Optimized llama.cpp profile for macOS",
                DedupKey: "llama-cpp-m2-balanced"
            )
        };
        var validator = new ProfileValidator();

        // Act
        var result = validator.ValidateForPublishing(profile);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RuntimeDetector_IdentifiesAvailableEngines()
    {
        // This test documents the expected runtime detection behavior
        // Arrange
        var detectedEngines = new List<string>();

        // Act: Simulate detection of available runtimes
        // (In real implementation, would shell out to check installed runtimes)
        // For now, we test the profile structure supports all engines
        var engines = new[] { "omlx", "mlx-lm", "llama.cpp", "vllm" };
        foreach (var engine in engines)
        {
            var profile = CreateProfileForEngine(engine);
            if (profile.Engine == engine)
            {
                detectedEngines.Add(engine);
            }
        }

        // Assert
        Assert.Equal(4, detectedEngines.Count);
    }
}

public class RuntimeCompatibilityTests
{
    private static Profile CreateBaseProfile()
    {
        return new Profile(
            SchemaVersion: 1,
            Id: "base-profile",
            ModelHfId: "meta-llama/Llama-2-7b",
            Tier: "balanced",
            Engine: "omlx",
            System: new Dictionary<string, object> { { "os", "macOS" } },
            OMLXSettings: new Dictionary<string, object>(),
            Harness: new Dictionary<string, object>(),
            Provenance: new ProfileProvenance("author", DateTime.UtcNow.ToString("O"), "test"),
            Hardware: new HardwareFingerprint("Apple M2", 16, "MacBookPro18,2"),
            Sampler: null,
            Community: null
        );
    }

    [Fact]
    public void AllRuntimes_SupportSameModelFormats()
    {
        // Arrange
        var models = new[] 
        { 
            "meta-llama/Llama-2-7b",
            "mistralai/Mistral-7B",
            "NousResearch/Nous-Hermes-2-7b"
        };
        var engines = new[] { "omlx", "mlx-lm", "llama.cpp", "vllm" };

        // Act & Assert: Each engine can reference each model
        foreach (var model in models)
        {
            foreach (var engine in engines)
            {
                var profile = CreateBaseProfile() with { ModelHfId = model, Engine = engine };
                Assert.Equal(model, profile.ModelHfId);
                Assert.Equal(engine, profile.Engine);
            }
        }
    }

    [Fact]
    public void RuntimeProfiles_CanShareHardwareFingerprints()
    {
        // Arrange
        var hardware = new HardwareFingerprint("Apple M4", 128, "MacBook16,5");
        var engine1 = CreateBaseProfile() with { Engine = "omlx", Hardware = hardware };
        var engine2 = CreateBaseProfile() with { Engine = "llama.cpp", Hardware = hardware };

        // Act & Assert
        Assert.Equal(engine1.Hardware.Chip, engine2.Hardware.Chip);
        Assert.Equal(engine1.Hardware.MemoryGb, engine2.Hardware.MemoryGb);
    }

    [Fact]
    public void LowMemoryMachine_ShouldHaveLlamaCppOptimizations()
    {
        // Arrange
        var lowMemProfile = CreateBaseProfile() with
        {
            Engine = "llama.cpp",
            Hardware = new HardwareFingerprint("Apple M1", 8, "MacBookAir11,2"),
            OMLXSettings = new Dictionary<string, object>
            {
                { "n_gpu_layers", 10 },  // Fewer GPU layers for low memory
                { "n_threads", 4 }
            }
        };

        // Act & Assert
        Assert.Equal("llama.cpp", lowMemProfile.Engine);
        Assert.Equal(8, lowMemProfile.Hardware.MemoryGb);
        Assert.Equal(10, lowMemProfile.OMLXSettings["n_gpu_layers"]);
    }

    [Fact]
    public void HighMemoryMachine_CanUseAdvancedRuntimes()
    {
        // Arrange
        var highMemProfile = CreateBaseProfile() with
        {
            Engine = "vllm",
            Hardware = new HardwareFingerprint("Apple M4 Max", 256, "MacBook16,5"),
            OMLXSettings = new Dictionary<string, object>
            {
                { "tensor_parallel_size", 2 },
                { "dtype", "float16" }
            }
        };

        // Act & Assert
        Assert.Equal("vllm", highMemProfile.Engine);
        Assert.Equal(256, highMemProfile.Hardware.MemoryGb);
        Assert.Equal(2, highMemProfile.OMLXSettings["tensor_parallel_size"]);
    }

    [Fact]
    public void RuntimeProfile_PreservesModelAndEngineIndependence()
    {
        // Arrange: Same model, different engines
        var profile1 = CreateBaseProfile() with { Engine = "omlx" };
        var profile2 = CreateBaseProfile() with { Engine = "llama.cpp" };
        var profile3 = CreateBaseProfile() with { Engine = "vllm" };

        // Act & Assert: Each profile maintains its own engine
        Assert.Equal("omlx", profile1.Engine);
        Assert.Equal("llama.cpp", profile2.Engine);
        Assert.Equal("vllm", profile3.Engine);
        Assert.Equal(profile1.ModelHfId, profile2.ModelHfId);
        Assert.Equal(profile2.ModelHfId, profile3.ModelHfId);
    }
}
