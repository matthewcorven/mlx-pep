using System.Diagnostics;
using MlxPep.Cli.Commands;
using MlxPep.Core;

namespace MlxPep.Cli.Tests;

public class DoctorCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WithJsonFlag_ReturnsValidJson()
    {
        // Arrange
        var command = new DoctorCommand();
        var context = new CommandContext(jsonOutput: true);

        // Act - capture stdout
        var oldOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        var result = await command.ExecuteAsync(context);

        Console.SetOut(oldOut);
        var output = sw.ToString();

        // Assert
        Assert.NotNull(result);
        Assert.True(output.Contains("command") && output.Contains("doctor"));
        Assert.Contains("status", output);
        Assert.Contains("dependencies", output);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutJsonFlag_ReturnsFormattedTable()
    {
        // Arrange
        var command = new DoctorCommand();
        var context = new CommandContext(jsonOutput: false);

        // Act - capture stdout
        var oldOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        var result = await command.ExecuteAsync(context);

        Console.SetOut(oldOut);
        var output = sw.ToString();

        // Assert
        Assert.NotNull(result);
        Assert.Contains("mlx-pep doctor", output);
        Assert.True(output.Contains("✓") || output.Contains("✗"));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccessCommandResult()
    {
        // Arrange
        var command = new DoctorCommand();
        var context = new CommandContext(jsonOutput: false);

        // Act - suppress output
        var oldOut = Console.Out;
        Console.SetOut(TextWriter.Null);

        var result = await command.ExecuteAsync(context);

        Console.SetOut(oldOut);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void DependencyDetectorService_DetectDotnet_ReturnsStatus()
    {
        // Arrange
        var detector = new DependencyDetectorService();

        // Act
        var result = detector.DetectDotnet();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<DependencyStatus>(result);
    }

    [Fact]
    public void DependencyDetectorService_DetectPython3_ReturnsStatus()
    {
        // Arrange
        var detector = new DependencyDetectorService();

        // Act
        var result = detector.DetectPython3();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<DependencyStatus>(result);
    }

    [Fact]
    public void DependencyDetectorService_DetectHfCli_ReturnsStatus()
    {
        // Arrange
        var detector = new DependencyDetectorService();

        // Act
        var result = detector.DetectHfCli();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<DependencyStatus>(result);
    }

    [Fact]
    public void DependencyDetectorService_DetectOmlx_ReturnsStatus()
    {
        // Arrange
        var detector = new DependencyDetectorService();

        // Act
        var result = detector.DetectOmlx();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<DependencyStatus>(result);
    }

    [Fact]
    public void DependencyDetectorService_DetectVsCode_ReturnsStatus()
    {
        // Arrange
        var detector = new DependencyDetectorService();

        // Act
        var result = detector.DetectVsCode();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<DependencyStatus>(result);
    }

    [Fact]
    public void DependencyDetectorService_DetectVsCodeInsiders_ReturnsStatus()
    {
        // Arrange
        var detector = new DependencyDetectorService();

        // Act
        var result = detector.DetectVsCodeInsiders();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<DependencyStatus>(result);
    }

    [Fact]
    public void DependencyDetectorService_DetectCopilotCli_ReturnsStatus()
    {
        // Arrange
        var detector = new DependencyDetectorService();

        // Act
        var result = detector.DetectCopilotCli();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<DependencyStatus>(result);
    }

    [Theory]
    [InlineData(true, "1.0.0")]
    [InlineData(false, null)]
    public void DependencyStatus_PropertiesSetCorrectly(bool installed, string? version)
    {
        // Arrange & Act
        var status = new DependencyStatus
        {
            Installed = installed,
            Version = version,
            Message = "Test message"
        };

        // Assert
        Assert.Equal(installed, status.Installed);
        Assert.Equal(version, status.Version);
        Assert.Equal("Test message", status.Message);
    }

    [Fact]
    public async Task ExecuteAsync_JsonOutput_IncludesAllDependencies()
    {
        // Arrange
        var command = new DoctorCommand();
        var context = new CommandContext(jsonOutput: true);

        // Act - capture stdout
        var oldOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        await command.ExecuteAsync(context);

        Console.SetOut(oldOut);
        var output = sw.ToString();

        // Assert - verify all dependencies are present in output
        Assert.Contains("dotnet", output);
        Assert.Contains("hf-cli", output);
        Assert.Contains("python3", output);
        Assert.Contains("omlx", output);
        Assert.Contains("vs-code", output);
        Assert.Contains("vs-code-insiders", output);
        Assert.Contains("copilot-cli", output);
    }

    [Fact]
    public async Task ExecuteAsync_TableOutput_IncludesReadableDependencyNames()
    {
        // Arrange
        var command = new DoctorCommand();
        var context = new CommandContext(jsonOutput: false);

        // Act - capture stdout
        var oldOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);

        await command.ExecuteAsync(context);

        Console.SetOut(oldOut);
        var output = sw.ToString();

        // Assert - verify readable names are present
        Assert.Contains(".NET SDK", output);
        Assert.Contains("Hugging Face CLI", output);
        Assert.Contains("Python 3", output);
        Assert.Contains("oMLX", output);
        Assert.Contains("VS Code", output);
        Assert.Contains("Copilot CLI", output);
    }
}
