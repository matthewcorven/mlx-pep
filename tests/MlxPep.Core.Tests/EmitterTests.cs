namespace MlxPep.Core.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using MlxPep.Core;
using MlxPep.Core.Emitters;

/// <summary>
/// Basic tests for profile emitters (Claude, OpenCode).
/// Comprehensive issue #8 tests are in Issue8ProfileSchemaTests.cs
/// Issue #27 (community metadata) tests are deferred.
/// </summary>
public class EmitterTests
{
    private static Profile CreateTestProfile()
    {
        return new Profile(
            SchemaVersion: 1,
            Id: "test-emitter-001",
            ModelHfId: "meta-llama/Llama-2-7b",
            Tier: "balanced",
            Engine: "mlx",
            System: new Dictionary<string, object> { { "os", "macOS" } },
            OMLXSettings: new Dictionary<string, object> { { "compute_units", "ALL" } },
            Harness: new Dictionary<string, object>
            {
                { "claude-code", new Dictionary<string, object>
                    {
                        { "maxInputTokens", 64000 },
                        { "maxOutputTokens", 16000 },
                        { "modelId", "claude-3-5-sonnet-20241022" },
                        { "temperature", 0.7 }
                    }
                }
            },
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
            Sampler: new SamplerSettings(
                Temperature: 0.7,
                TopP: 0.9,
                TopK: 40,
                RepetitionPenalty: null,
                ContextTokens: null
            )
        );
    }

    [Fact]
    public async Task ClaudeCodeEmitter_GeneratesValidJsonConfig()
    {
        // Arrange
        var emitter = new ClaudeCodeEmitter();
        var profile = CreateTestProfile();

        // Act
        var configJson = await emitter.EmitAsync(profile);

        // Assert
        Assert.NotNull(configJson);
        Assert.Contains("claude", configJson.ToLowerInvariant());
    }

    [Fact]
    public async Task OpenCodeEmitter_GeneratesValidJsonConfig()
    {
        // Arrange
        var emitter = new OpenCodeEmitter();
        var profile = CreateTestProfile() with
        {
            Harness = new Dictionary<string, object>
            {
                { "opencode", new Dictionary<string, object>
                    {
                        { "temperature", 0.7 }
                    }
                }
            }
        };

        // Act
        var configJson = await emitter.EmitAsync(profile);

        // Assert
        Assert.NotNull(configJson);
        Assert.NotEmpty(configJson);
    }
}
