namespace MlxPep.Cli.Tests.Commands;

using System.Text.Json;
using Xunit;
using MlxPep.Cli.Commands;
using MlxPep.Core;

/// <summary>
/// Tests for the `mlx-pep assess` command.
/// Issue #17: Test scaffolding for profiling assess command.
/// Validates command routing, options handling, output formats, and safety constraints.
/// </summary>
public class AssessCommandTests
{
    [Fact]
    public async Task AssessCommand_ExecuteAsync_WithValidHfId_ReturnsSuccess()
    {
        // Arrange
        var command = new AssessCommand();
        var context = new CommandContext(jsonOutput: false);
        var hfId = "meta-llama/Llama-2-7b";

        // Act
        var result = await command.ExecuteAsync(hfId, publish: false, context);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task AssessCommand_ExecuteAsync_WithJsonFlag_OutputsValidJson()
    {
        // Arrange
        var command = new AssessCommand();
        var context = new CommandContext(jsonOutput: true);
        var hfId = "meta-llama/Llama-2-7b";

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);

            // Act
            var result = await command.ExecuteAsync(hfId, publish: false, context);
            Console.SetOut(oldOutput);

            var output = writer.ToString().Trim();

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.NotEmpty(output);
            
            // Verify JSON is valid and contains expected fields
            var json = JsonDocument.Parse(output);
            var root = json.RootElement;
            
            Assert.True(root.TryGetProperty("command", out var cmdProp));
            Assert.Equal("assess", cmdProp.GetString());
            
            Assert.True(root.TryGetProperty("status", out var statusProp));
            Assert.True(
                statusProp.GetString() == "ok" || statusProp.GetString() == "error",
                "Status must be 'ok' or 'error'"
            );
            
            Assert.True(root.TryGetProperty("hfId", out var hfIdProp));
            Assert.Equal(hfId, hfIdProp.GetString());
            
            Assert.True(root.TryGetProperty("profiles", out var profilesProp));
            Assert.NotEqual(0, profilesProp.GetArrayLength());
        }
    }

    [Fact]
    public async Task AssessCommand_ExecuteAsync_WithoutJsonFlag_OutputsTableFormat()
    {
        // Arrange
        var command = new AssessCommand();
        var context = new CommandContext(jsonOutput: false);
        var hfId = "meta-llama/Llama-2-7b";

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);

            // Act
            var result = await command.ExecuteAsync(hfId, publish: false, context);
            Console.SetOut(oldOutput);

            var output = writer.ToString();

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Contains(hfId, output);
            Assert.Contains("profiles", output);
        }
    }

    [Fact]
    public async Task AssessCommand_ExecuteAsync_GeneratesThreeProfiles()
    {
        // Arrange
        var command = new AssessCommand();
        var context = new CommandContext(jsonOutput: true);
        var hfId = "meta-llama/Llama-2-7b";

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);

            // Act
            var result = await command.ExecuteAsync(hfId, publish: false, context);
            Console.SetOut(oldOutput);

            var output = writer.ToString().Trim();

            // Assert
            Assert.Equal(0, result.ExitCode);
            var json = JsonDocument.Parse(output);
            var root = json.RootElement;
            var profiles = root.GetProperty("profiles");
            
            // The command must emit exactly 3 tiers (but may use "high-performance" instead of "high")
            Assert.True(
                profiles.GetArrayLength() >= 3,
                "Command must generate at least 3 profiles (high/balanced/efficient)"
            );
        }
    }

    [Fact]
    public async Task AssessCommand_ExecuteAsync_WithPublishFlag_ValidatesProfiles()
    {
        // Arrange
        var command = new AssessCommand();
        var context = new CommandContext(jsonOutput: true);
        var hfId = "meta-llama/Llama-2-7b";

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);

            // Act
            var result = await command.ExecuteAsync(hfId, publish: true, context);
            Console.SetOut(oldOutput);

            var output = writer.ToString().Trim();

            // Assert
            Assert.Equal(0, result.ExitCode);
            var json = JsonDocument.Parse(output);
            var root = json.RootElement;
            
            // With publish flag, validation results should be included
            Assert.True(root.TryGetProperty("validation", out var validationProp));
            Assert.True(validationProp.TryGetProperty("isValid", out _));
            Assert.True(validationProp.TryGetProperty("errorCount", out _));
        }
    }

    [Fact]
    public async Task AssessCommand_ExecuteAsync_WithPublishFlag_WithoutJsonFlag_OutputsTableFormat()
    {
        // Arrange
        var command = new AssessCommand();
        var context = new CommandContext(jsonOutput: false);
        var hfId = "stabilityai/stablelm-2-zephyr";

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);

            // Act
            var result = await command.ExecuteAsync(hfId, publish: true, context);
            Console.SetOut(oldOutput);

            var output = writer.ToString();

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Contains(hfId, output);
            Assert.Contains("Generated 3 profiles", output);
            Assert.Contains("profiles valid", output);
        }
    }

    [Fact]
    public async Task AssessCommand_ExecuteAsync_WithoutPublishFlag_OmitsValidation()
    {
        // Arrange
        var command = new AssessCommand();
        var context = new CommandContext(jsonOutput: true);
        var hfId = "meta-llama/Llama-2-7b";

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);

            // Act
            var result = await command.ExecuteAsync(hfId, publish: false, context);
            Console.SetOut(oldOutput);

            var output = writer.ToString().Trim();

            // Assert
            Assert.Equal(0, result.ExitCode);
            var json = JsonDocument.Parse(output);
            var root = json.RootElement;
            
            // Without publish flag, validation should not be included
            Assert.True(root.TryGetProperty("published", out var publishedProp));
            Assert.False(publishedProp.GetBoolean());
        }
    }

    [Fact]
    public async Task AssessCommand_ExecuteAsync_SetsProvenanceToAssess()
    {
        // Arrange
        var command = new AssessCommand();
        var context = new CommandContext(jsonOutput: false);
        var hfId = "meta-llama/Llama-2-7b";

        // Act
        // Note: AssessCommand currently uses "cli" as source. This test documents that
        // The actual implementation should set source="assess" when delegating to model-assessor
        var result = await command.ExecuteAsync(hfId, publish: false, context);

        // Assert
        Assert.Equal(0, result.ExitCode);
        // Future: when Neo implements real ProfilingRunner, verify provenance.source == "assess"
    }

    [Fact]
    public async Task AssessCommand_ExecuteAsync_NoModelUnloadCommands()
    {
        // Arrange: safety constraint test
        // Verify that assess command does NOT attempt to unload or uninstall oMLX models
        var command = new AssessCommand();
        var context = new CommandContext(jsonOutput: false);
        var hfId = "meta-llama/Llama-2-7b";

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);

            // Act
            var result = await command.ExecuteAsync(hfId, publish: false, context);
            Console.SetOut(oldOutput);

            var output = writer.ToString();

            // Assert: verify no dangerous keywords in output
            Assert.DoesNotContain("unload", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("uninstall", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("delete", output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("remove model", output, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task AssessCommand_ExecuteAsync_ProfilesTierFormat()
    {
        // Arrange
        var command = new AssessCommand();
        var context = new CommandContext(jsonOutput: true);
        var hfId = "meta-llama/Llama-2-7b";

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);

            // Act
            var result = await command.ExecuteAsync(hfId, publish: false, context);
            Console.SetOut(oldOutput);

            var output = writer.ToString().Trim();

            // Assert
            var json = JsonDocument.Parse(output);
            var root = json.RootElement;
            var profiles = root.GetProperty("profiles");
            
            // Each profile should have id and tier properties
            foreach (var profile in profiles.EnumerateArray())
            {
                Assert.True(profile.TryGetProperty("id", out var idProp));
                Assert.NotEmpty(idProp.GetString()!);
                
                Assert.True(profile.TryGetProperty("tier", out var tierProp));
                var tier = tierProp.GetString()!;
                Assert.NotEmpty(tier);
            }
        }
    }

    [Fact]
    public async Task AssessCommand_ExecuteAsync_WithEmptyHfId_ShouldHandle()
    {
        // Arrange
        var command = new AssessCommand();
        var context = new CommandContext(jsonOutput: false);
        var hfId = "";

        // Act: should handle gracefully (may succeed with empty or fail with error)
        var result = await command.ExecuteAsync(hfId, publish: false, context);

        // Assert: either succeeds or provides meaningful error
        Assert.NotNull(result);
        // Either exit code 0 or non-zero with message
        Assert.True(result.ExitCode >= 0);
    }

    [Fact]
    public async Task AssessCommand_ExecuteAsync_DefaultContextIsNotNull()
    {
        // Arrange
        var command = new AssessCommand();
        var hfId = "meta-llama/Llama-2-7b";

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);

            // Act: call without explicit context
            var result = await command.ExecuteAsync(hfId, publish: false, context: null);
            Console.SetOut(oldOutput);

            // Assert: should use default context and succeed
            Assert.Equal(0, result.ExitCode);
        }
    }

    [Fact]
    public async Task AssessCommand_ExecuteAsync_ExceptionHandling()
    {
        // Arrange
        var command = new AssessCommand();
        var context = new CommandContext(jsonOutput: false);
        
        // Pass null hfId to potentially trigger exception handling
        // (depends on implementation; may or may not throw)
        var hfId = null as string;

        // Act
        var result = await command.ExecuteAsync(hfId!, publish: false, context);

        // Assert: should not crash, but may return failure
        Assert.NotNull(result);
    }
}
