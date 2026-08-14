namespace MlxPep.Cli.Tests.Commands;

using System.Text.Json;
using MlxPep.Cli.Commands;

public class DoctorCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WithJsonFlag_OutputsValidJson()
    {
        // Arrange
        var command = new DoctorCommand();
        var context = new CommandContext { JsonOutput = true };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);

            // Act
            var result = await command.ExecuteAsync(context);
            Console.SetOut(oldOutput);

            var output = writer.ToString();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.ExitCode);
            var json = JsonDocument.Parse(output);
            Assert.NotNull(json);
            var root = json.RootElement;
            Assert.True(root.TryGetProperty("command", out var cmdProp));
            Assert.Equal("doctor", cmdProp.GetString());
            Assert.True(root.TryGetProperty("dependencies", out _));
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithoutJsonFlag_OutputsTable()
    {
        // Arrange
        var command = new DoctorCommand();
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);

            // Act
            var result = await command.ExecuteAsync(context);
            Console.SetOut(oldOutput);

            var output = writer.ToString();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("mlx-pep doctor", output);
            Assert.Contains("Summary:", output);
            Assert.Contains("installed", output);
            Assert.Contains("missing", output);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess()
    {
        // Arrange
        var command = new DoctorCommand();
        var context = new CommandContext { JsonOutput = false };

        // Act
        var result = await command.ExecuteAsync(context);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_JsonOutput_IncludesAllDependencies()
    {
        // Arrange
        var command = new DoctorCommand();
        var context = new CommandContext { JsonOutput = true };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);

            // Act
            var result = await command.ExecuteAsync(context);
            Console.SetOut(oldOutput);

            var output = writer.ToString();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("dotnet", output);
            Assert.Contains("python3", output);
            Assert.Contains("hf-cli", output);
            Assert.Contains("omlx", output);
            Assert.Contains("vscode", output);
            Assert.Contains("vscode-insiders", output);
            Assert.Contains("copilot-cli", output);
        }
    }

    [Fact]
    public async Task ExecuteAsync_TableOutput_DisplaysCorrectNames()
    {
        // Arrange
        var command = new DoctorCommand();
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);

            // Act
            var result = await command.ExecuteAsync(context);
            Console.SetOut(oldOutput);

            var output = writer.ToString();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains(".NET", output);
            Assert.Contains("Python 3", output);
            Assert.Contains("Hugging Face CLI", output);
            Assert.Contains("oMLX", output);
            Assert.Contains("VS Code", output);
            Assert.Contains("VS Code Insiders", output);
            Assert.Contains("Copilot CLI", output);
        }
    }

    [Fact]
    public async Task ExecuteAsync_TableOutput_ShowsInstallationGuidance()
    {
        // Arrange
        var command = new DoctorCommand();
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);

            // Act
            var result = await command.ExecuteAsync(context);
            Console.SetOut(oldOutput);

            var output = writer.ToString();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.ExitCode);
            if (output.Contains("missing"))
            {
                Assert.Contains("--json", output);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_JsonOutput_ValidStructure()
    {
        // Arrange
        var command = new DoctorCommand();
        var context = new CommandContext { JsonOutput = true };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);

            // Act
            var result = await command.ExecuteAsync(context);
            Console.SetOut(oldOutput);

            var output = writer.ToString();

            // Assert
            var json = JsonDocument.Parse(output);
            var root = json.RootElement;
            Assert.True(root.TryGetProperty("timestamp", out var timestamp));
            Assert.NotEqual(default, timestamp.ValueKind);
            Assert.True(root.TryGetProperty("dependencies", out var deps));
            Assert.Equal(JsonValueKind.Object, deps.ValueKind);
        }
    }

    [Fact]
    public async Task ExecuteAsync_JsonOutput_DependencyHasCorrectFields()
    {
        // Arrange
        var command = new DoctorCommand();
        var context = new CommandContext { JsonOutput = true };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);

            // Act
            var result = await command.ExecuteAsync(context);
            Console.SetOut(oldOutput);

            var output = writer.ToString();

            // Assert
            var json = JsonDocument.Parse(output);
            var root = json.RootElement;
            var deps = root.GetProperty("dependencies");
            var dotnet = deps.GetProperty("dotnet");

            Assert.True(dotnet.TryGetProperty("installed", out _));
            // version and message are optional
        }
    }

    [Fact]
    public async Task ExecuteAsync_TableOutput_ContainsStatusSymbols()
    {
        // Arrange
        var command = new DoctorCommand();
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);

            // Act
            var result = await command.ExecuteAsync(context);
            Console.SetOut(oldOutput);

            var output = writer.ToString();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.ExitCode);
            // At least one checkmark or X should be present
            Assert.True(output.Contains("✓") || output.Contains("✗"));
        }
    }
}
