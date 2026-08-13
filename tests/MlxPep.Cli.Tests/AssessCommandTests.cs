namespace MlxPep.Cli.Tests;

using Xunit;
using MlxPep.Core;
using MlxPep.Cli.Commands;
using System.IO;

public class AssessCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WithFixtureRunner_ReturnsSuccess()
    {
        // Arrange
        var fixtureRunner = new FixtureProfilingRunner();
        var tempDir = Path.Combine(Path.GetTempPath(), $"mlx-pep-test-{Guid.NewGuid()}");
        
        try
        {
            var command = new AssessCommand(
                profilingRunner: fixtureRunner,
                profilesDirectory: tempDir);
            var context = new CommandContext(jsonOutput: false);

            // Act
            var result = await command.ExecuteAsync(
                hfId: "meta-llama/Llama-2-7b",
                assistantModelId: null,
                suite: "smoke",
                publish: false,
                context: context);

            // Assert
            Assert.True(result.ExitCode == 0);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_SavesThreeProfilesToDisk()
    {
        // Arrange
        var fixtureRunner = new FixtureProfilingRunner();
        var tempDir = Path.Combine(Path.GetTempPath(), $"mlx-pep-test-{Guid.NewGuid()}");
        
        try
        {
            var command = new AssessCommand(
                profilingRunner: fixtureRunner,
                profilesDirectory: tempDir);
            var context = new CommandContext(jsonOutput: false);

            // Act
            var result = await command.ExecuteAsync(
                hfId: "meta-llama/Llama-2-7b",
                assistantModelId: null,
                suite: "smoke",
                publish: false,
                context: context);

            // Assert
            Assert.True(result.ExitCode == 0);
            Assert.True(Directory.Exists(tempDir));
            
            var files = Directory.GetFiles(tempDir, "*.jsonl");
            Assert.Equal(3, files.Length);
            
            // Check that each file contains valid JSON
            foreach (var file in files)
            {
                var content = File.ReadAllText(file);
                Assert.NotEmpty(content);
                Assert.Contains("\"modelHfId\"", content);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_MapsHighPerformanceTier()
    {
        // Arrange
        var fixtureRunner = new FixtureProfilingRunner();
        var tempDir = Path.Combine(Path.GetTempPath(), $"mlx-pep-test-{Guid.NewGuid()}");
        
        try
        {
            var command = new AssessCommand(
                profilingRunner: fixtureRunner,
                profilesDirectory: tempDir);
            var context = new CommandContext(jsonOutput: false);

            // Act
            await command.ExecuteAsync(
                hfId: "meta-llama/Llama-2-7b",
                assistantModelId: null,
                suite: "smoke",
                publish: false,
                context: context);

            // Assert
            var files = Directory.GetFiles(tempDir, "*high-performance*.jsonl");
            Assert.Single(files);
            
            var content = File.ReadAllText(files[0]);
            Assert.Contains("high-performance", content);
            Assert.Contains("\"tier\":\"high-performance\"", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_MapsBalancedTier()
    {
        // Arrange
        var fixtureRunner = new FixtureProfilingRunner();
        var tempDir = Path.Combine(Path.GetTempPath(), $"mlx-pep-test-{Guid.NewGuid()}");
        
        try
        {
            var command = new AssessCommand(
                profilingRunner: fixtureRunner,
                profilesDirectory: tempDir);
            var context = new CommandContext(jsonOutput: false);

            // Act
            await command.ExecuteAsync(
                hfId: "meta-llama/Llama-2-7b",
                assistantModelId: null,
                suite: "smoke",
                publish: false,
                context: context);

            // Assert
            var files = Directory.GetFiles(tempDir, "*balanced*.jsonl");
            Assert.Single(files);
            
            var content = File.ReadAllText(files[0]);
            Assert.Contains("balanced", content);
            Assert.Contains("\"tier\":\"balanced\"", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_MapsEfficientTier()
    {
        // Arrange
        var fixtureRunner = new FixtureProfilingRunner();
        var tempDir = Path.Combine(Path.GetTempPath(), $"mlx-pep-test-{Guid.NewGuid()}");
        
        try
        {
            var command = new AssessCommand(
                profilingRunner: fixtureRunner,
                profilesDirectory: tempDir);
            var context = new CommandContext(jsonOutput: false);

            // Act
            await command.ExecuteAsync(
                hfId: "meta-llama/Llama-2-7b",
                assistantModelId: null,
                suite: "smoke",
                publish: false,
                context: context);

            // Assert
            var files = Directory.GetFiles(tempDir, "*efficient*.jsonl");
            Assert.Single(files);
            
            var content = File.ReadAllText(files[0]);
            Assert.Contains("efficient", content);
            Assert.Contains("\"tier\":\"efficient\"", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_IncludesOMLXSettings()
    {
        // Arrange
        var fixtureRunner = new FixtureProfilingRunner();
        var tempDir = Path.Combine(Path.GetTempPath(), $"mlx-pep-test-{Guid.NewGuid()}");
        
        try
        {
            var command = new AssessCommand(
                profilingRunner: fixtureRunner,
                profilesDirectory: tempDir);
            var context = new CommandContext(jsonOutput: false);

            // Act
            await command.ExecuteAsync(
                hfId: "meta-llama/Llama-2-7b",
                assistantModelId: null,
                suite: "smoke",
                publish: false,
                context: context);

            // Assert
            var files = Directory.GetFiles(tempDir, "*.jsonl");
            foreach (var file in files)
            {
                var content = File.ReadAllText(file);
                Assert.Contains("\"omlx\"", content);
                Assert.Contains("compute_units", content);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithPublish_ValidatesProfiles()
    {
        // Arrange
        var fixtureRunner = new FixtureProfilingRunner();
        var tempDir = Path.Combine(Path.GetTempPath(), $"mlx-pep-test-{Guid.NewGuid()}");
        
        try
        {
            var command = new AssessCommand(
                profilingRunner: fixtureRunner,
                profilesDirectory: tempDir);
            var context = new CommandContext(jsonOutput: false);

            // Act
            var result = await command.ExecuteAsync(
                hfId: "meta-llama/Llama-2-7b",
                assistantModelId: null,
                suite: "smoke",
                publish: true,
                context: context);

            // Assert
            Assert.True(result.ExitCode == 0);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithJsonOutput_ReturnsValidJson()
    {
        // Arrange
        var fixtureRunner = new FixtureProfilingRunner();
        var tempDir = Path.Combine(Path.GetTempPath(), $"mlx-pep-test-{Guid.NewGuid()}");
        
        try
        {
            var command = new AssessCommand(
                profilingRunner: fixtureRunner,
                profilesDirectory: tempDir);
            var context = new CommandContext(jsonOutput: true);

            // Act
            var result = await command.ExecuteAsync(
                hfId: "meta-llama/Llama-2-7b",
                assistantModelId: null,
                suite: "smoke",
                publish: false,
                context: context);

            // Assert
            Assert.True(result.ExitCode == 0);
            // JSON output is written to Console.WriteLine, so we just verify success here
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ProfileContainsCorrectSchema()
    {
        // Arrange
        var fixtureRunner = new FixtureProfilingRunner();
        var tempDir = Path.Combine(Path.GetTempPath(), $"mlx-pep-test-{Guid.NewGuid()}");
        
        try
        {
            var command = new AssessCommand(
                profilingRunner: fixtureRunner,
                profilesDirectory: tempDir);
            var context = new CommandContext(jsonOutput: false);

            // Act
            await command.ExecuteAsync(
                hfId: "meta-llama/Llama-2-7b",
                assistantModelId: null,
                suite: "smoke",
                publish: false,
                context: context);

            // Assert
            var files = Directory.GetFiles(tempDir, "*.jsonl");
            var content = File.ReadAllText(files[0]);
            
            // Verify required schema fields
            Assert.Contains("\"schemaVersion\"", content);
            Assert.Contains("\"id\"", content);
            Assert.Contains("\"modelHfId\"", content);
            Assert.Contains("\"tier\"", content);
            Assert.Contains("\"engine\"", content);
            Assert.Contains("\"system\"", content);
            Assert.Contains("\"omlx\"", content);
            Assert.Contains("\"harness\"", content);
            Assert.Contains("\"provenance\"", content);
            Assert.Contains("\"hardware\"", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
