using MlxPep.Core.Diagnostics;

namespace MlxPep.Core.Tests.Diagnostics;

public class DependencyDetectionServiceTests
{
    [Fact]
    public async Task DetectAsync_WithAllToolsInstalled_ReturnsSuccessReport()
    {
        // Arrange
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: true, rawOutput: "10.0.0") },
            { "hf-cli", new MockProbe(found: true, rawOutput: "huggingface_hub version: 0.19.0") },
            { "python3", new MockProbe(found: true, rawOutput: "Python 3.11.0") },
            { "copilot-cli", new MockProbe(found: true, rawOutput: "gh version 2.45.0") },
            { "vscode", new MockProbe(found: true, rawOutput: "/Applications/Visual Studio Code.app") },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: true, rawOutput: "localhost:8000") }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert
        Assert.NotNull(report);
        Assert.Equal(DependencyReportStatus.Success, report.Status);
        Assert.True(report.Tools.ContainsKey("dotnet"));
        Assert.True(report.Tools["dotnet"].Installed);
        Assert.Equal("10.0.0", report.Tools["dotnet"].Version);
    }

    [Fact]
    public async Task DetectAsync_WithMissingTools_ReturnsInstalledFalse()
    {
        // Arrange
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: false, error: "Not found in PATH") },
            { "hf-cli", new MockProbe(found: false, error: "Not found in PATH") },
            { "python3", new MockProbe(found: false, error: "Not found in PATH") },
            { "copilot-cli", new MockProbe(found: false, error: "Not found in PATH") },
            { "vscode", new MockProbe(found: false) },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: false) }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert
        Assert.NotNull(report);
        Assert.False(report.Tools["dotnet"].Installed);
        Assert.False(report.Tools["hf-cli"].Installed);
        Assert.NotNull(report.Tools["dotnet"].InstallGuidance);
    }

    [Fact]
    public async Task DotnetDetection_ParsesVersionCorrectly()
    {
        // Arrange
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: true, rawOutput: "10.0.0") },
            { "hf-cli", new MockProbe(found: false) },
            { "python3", new MockProbe(found: false) },
            { "copilot-cli", new MockProbe(found: false) },
            { "vscode", new MockProbe(found: false) },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: false) }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert
        Assert.True(report.Tools["dotnet"].Installed);
        Assert.Equal("10.0.0", report.Tools["dotnet"].Version);
    }

    [Fact]
    public async Task Python3Detection_ParsesVersionCorrectly()
    {
        // Arrange
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: false) },
            { "hf-cli", new MockProbe(found: false) },
            { "python3", new MockProbe(found: true, rawOutput: "Python 3.11.8") },
            { "copilot-cli", new MockProbe(found: false) },
            { "vscode", new MockProbe(found: false) },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: false) }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert
        Assert.True(report.Tools["python3"].Installed);
        Assert.Equal("3.11.8", report.Tools["python3"].Version);
    }

    [Fact]
    public async Task HfCliDetection_ParsesVersionCorrectly()
    {
        // Arrange
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: false) },
            { "hf-cli", new MockProbe(found: true, rawOutput: "huggingface_hub version: 0.19.0") },
            { "python3", new MockProbe(found: false) },
            { "copilot-cli", new MockProbe(found: false) },
            { "vscode", new MockProbe(found: false) },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: false) }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert
        Assert.True(report.Tools["hf-cli"].Installed);
        Assert.Equal("0.19.0", report.Tools["hf-cli"].Version);
    }

    [Fact]
    public async Task OmlxDetection_DetectsAppBundle()
    {
        // Arrange
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: false) },
            { "hf-cli", new MockProbe(found: false) },
            { "python3", new MockProbe(found: false) },
            { "copilot-cli", new MockProbe(found: false) },
            { "vscode", new MockProbe(found: false) },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: true, rawOutput: "/Applications/oMLX.app") }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert
        Assert.True(report.Tools["omlx"].Installed);
        Assert.Equal("app-bundle", report.Tools["omlx"].Scope);
    }

    [Fact]
    public async Task OmlxDetection_DetectsRunningServer()
    {
        // Arrange
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: false) },
            { "hf-cli", new MockProbe(found: false) },
            { "python3", new MockProbe(found: false) },
            { "copilot-cli", new MockProbe(found: false) },
            { "vscode", new MockProbe(found: false) },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: true, rawOutput: "localhost:8000") }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert
        Assert.True(report.Tools["omlx"].Installed);
        Assert.Equal("running", report.Tools["omlx"].Scope);
    }

    [Fact]
    public async Task VsCodeDetection_DetectsAppBundle()
    {
        // Arrange
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: false) },
            { "hf-cli", new MockProbe(found: false) },
            { "python3", new MockProbe(found: false) },
            { "copilot-cli", new MockProbe(found: false) },
            { "vscode", new MockProbe(found: true, rawOutput: "/Applications/Visual Studio Code.app") },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: false) }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert
        Assert.True(report.Tools["vscode"].Installed);
        Assert.NotNull(report.Tools["vscode"].ToolPath);
    }

    [Fact]
    public async Task CopilotCliDetection_ParsesVersionCorrectly()
    {
        // Arrange
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: false) },
            { "hf-cli", new MockProbe(found: false) },
            { "python3", new MockProbe(found: false) },
            { "copilot-cli", new MockProbe(found: true, rawOutput: "gh version 2.45.0 (2024-01-01)") },
            { "vscode", new MockProbe(found: false) },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: false) }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert
        Assert.True(report.Tools["copilot-cli"].Installed);
        Assert.Equal("2.45.0", report.Tools["copilot-cli"].Version);
    }

    [Fact]
    public async Task MissingTool_ProvideInstallGuidance()
    {
        // Arrange
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: false, error: "Not found in PATH") },
            { "hf-cli", new MockProbe(found: false) },
            { "python3", new MockProbe(found: false) },
            { "copilot-cli", new MockProbe(found: false) },
            { "vscode", new MockProbe(found: false) },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: false) }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert
        Assert.False(report.Tools["dotnet"].Installed);
        Assert.NotNull(report.Tools["dotnet"].InstallGuidance);
        Assert.Contains("brew install", report.Tools["dotnet"].InstallGuidance);
    }

    [Fact]
    public async Task ReportIncludesModelAssessor()
    {
        // Arrange
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: false) },
            { "hf-cli", new MockProbe(found: false) },
            { "python3", new MockProbe(found: false) },
            { "copilot-cli", new MockProbe(found: false) },
            { "vscode", new MockProbe(found: false) },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: false) }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert
        Assert.True(report.Tools.ContainsKey("model-assessor"));
    }

    [Fact]
    public async Task ReportWithErrorsHasPartialSuccessStatus()
    {
        // Arrange
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: false) },
            { "hf-cli", new MockProbe(found: false) },
            { "python3", new MockProbe(found: false) },
            { "copilot-cli", new MockProbe(found: false) },
            { "vscode", new MockProbe(found: false) },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: false) }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert
        Assert.Equal(DependencyReportStatus.PartialSuccess, report.Status);
    }

    [Fact]
    public async Task ReportIncludesAllEightTools()
    {
        // Arrange
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: true, rawOutput: "10.0.0") },
            { "hf-cli", new MockProbe(found: true, rawOutput: "0.19.0") },
            { "python3", new MockProbe(found: true, rawOutput: "3.11.0") },
            { "copilot-cli", new MockProbe(found: true, rawOutput: "2.45.0") },
            { "vscode", new MockProbe(found: true, rawOutput: "/Applications/Visual Studio Code.app") },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: true, rawOutput: "localhost:8000") }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert
        Assert.Equal(8, report.Tools.Count);
        Assert.True(report.Tools.ContainsKey("dotnet"));
        Assert.True(report.Tools.ContainsKey("hf-cli"));
        Assert.True(report.Tools.ContainsKey("python3"));
        Assert.True(report.Tools.ContainsKey("model-assessor"));
        Assert.True(report.Tools.ContainsKey("omlx"));
        Assert.True(report.Tools.ContainsKey("vscode"));
        Assert.True(report.Tools.ContainsKey("vscode-insiders"));
        Assert.True(report.Tools.ContainsKey("copilot-cli"));
    }

    [Fact]
    public async Task ToolStatus_HasAllRequiredProperties()
    {
        // Arrange
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: true, rawOutput: "10.0.0") },
            { "hf-cli", new MockProbe(found: false) },
            { "python3", new MockProbe(found: false) },
            { "copilot-cli", new MockProbe(found: false) },
            { "vscode", new MockProbe(found: false) },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: false) }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();
        var dotnetStatus = report.Tools["dotnet"];

        // Assert
        Assert.NotNull(dotnetStatus.Name);
        Assert.NotNull(dotnetStatus.DisplayName);
        Assert.True(dotnetStatus.Installed);
        Assert.NotNull(dotnetStatus.Version);
    }

    [Fact]
    public async Task DetectAsync_ReturnsTimestamp()
    {
        // Arrange
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: false) },
            { "hf-cli", new MockProbe(found: false) },
            { "python3", new MockProbe(found: false) },
            { "copilot-cli", new MockProbe(found: false) },
            { "vscode", new MockProbe(found: false) },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: false) }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert
        Assert.True(report.GeneratedAt > DateTime.UtcNow.AddSeconds(-10));
    }

    // Edge case tests for version parsing robustness
    [Fact]
    public async Task VersionParsing_HandlesEmptyOutput()
    {
        // Arrange
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: true, rawOutput: "") },
            { "hf-cli", new MockProbe(found: false) },
            { "python3", new MockProbe(found: false) },
            { "copilot-cli", new MockProbe(found: false) },
            { "vscode", new MockProbe(found: false) },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: false) }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert - empty output means found but version is null
        Assert.True(report.Tools["dotnet"].Installed);
        Assert.Null(report.Tools["dotnet"].Version);
    }

    [Fact]
    public async Task VersionParsing_HandlesWhitespaceVariations()
    {
        // Arrange
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: true, rawOutput: "  10.0.0  \n\n") },
            { "hf-cli", new MockProbe(found: false) },
            { "python3", new MockProbe(found: false) },
            { "copilot-cli", new MockProbe(found: false) },
            { "vscode", new MockProbe(found: false) },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: false) }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert - whitespace should be handled gracefully
        Assert.True(report.Tools["dotnet"].Installed);
        Assert.Equal("10.0.0", report.Tools["dotnet"].Version);
    }

    [Fact]
    public async Task VersionParsing_HandlesPrereleaseVersions()
    {
        // Arrange: Test with prerelease version format (e.g., "1.0.0-beta.1")
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: true, rawOutput: "10.0.0-preview.5") },
            { "hf-cli", new MockProbe(found: false) },
            { "python3", new MockProbe(found: false) },
            { "copilot-cli", new MockProbe(found: false) },
            { "vscode", new MockProbe(found: false) },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: false) }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert - regex should extract base version "10.0.0" from prerelease format
        Assert.True(report.Tools["dotnet"].Installed);
        // The regex pattern (\d+\.\d+\.\d+) should match "10.0.0" from "10.0.0-preview.5"
        Assert.Equal("10.0.0", report.Tools["dotnet"].Version);
    }

    [Fact]
    public async Task VersionParsing_HandlesTwoPartVersions()
    {
        // Arrange: Some tools might return 2-part versions like "1.2" instead of "1.2.3"
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: true, rawOutput: "10.0") },
            { "hf-cli", new MockProbe(found: false) },
            { "python3", new MockProbe(found: false) },
            { "copilot-cli", new MockProbe(found: false) },
            { "vscode", new MockProbe(found: false) },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: false) }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert - regex requires 3 parts, so 2-part version won't match and version will be null
        Assert.True(report.Tools["dotnet"].Installed);
        // Note: This is a limitation of the current regex pattern - two-part versions won't parse
        Assert.Null(report.Tools["dotnet"].Version);
    }

    [Fact]
    public async Task VersionParsing_HandlesMultilineOutput()
    {
        // Arrange: Some tools output multiple lines; only first line should be used
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: true, rawOutput: "10.0.0\nAdditional info\nMore info") },
            { "hf-cli", new MockProbe(found: false) },
            { "python3", new MockProbe(found: false) },
            { "copilot-cli", new MockProbe(found: false) },
            { "vscode", new MockProbe(found: false) },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: false) }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert - version should extract from first line
        Assert.True(report.Tools["dotnet"].Installed);
        Assert.Equal("10.0.0", report.Tools["dotnet"].Version);
    }

    [Fact]
    public async Task ProbeError_StoresErrorMessage()
    {
        // Arrange: Probe reports found:false with error message
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: false, error: "Command 'dotnet' not found in PATH") },
            { "hf-cli", new MockProbe(found: false) },
            { "python3", new MockProbe(found: false) },
            { "copilot-cli", new MockProbe(found: false) },
            { "vscode", new MockProbe(found: false) },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: false) }
        };
        var service = new DependencyDetectionService(probes);

        // Act
        var report = await service.DetectAsync();

        // Assert - error message should be preserved
        Assert.False(report.Tools["dotnet"].Installed);
        Assert.NotNull(report.Tools["dotnet"].Message);
        Assert.Contains("not found", report.Tools["dotnet"].Message);
    }

    [Fact]
    public async Task JsonSerialization_RoundTrips()
    {
        // Arrange
        var probes = new Dictionary<string, IDependencyProbe>
        {
            { "dotnet", new MockProbe(found: true, rawOutput: "10.0.0") },
            { "hf-cli", new MockProbe(found: true, rawOutput: "huggingface_hub version: 0.19.0") },
            { "python3", new MockProbe(found: false) },
            { "copilot-cli", new MockProbe(found: false) },
            { "vscode", new MockProbe(found: true, rawOutput: "/Applications/Visual Studio Code.app") },
            { "vscode-insiders", new MockProbe(found: false) },
            { "omlx", new MockProbe(found: false) }
        };
        var service = new DependencyDetectionService(probes);
        var report = await service.DetectAsync();

        // Act - serialize and deserialize
        var json = System.Text.Json.JsonSerializer.Serialize(report);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<DependencyReport>(json);

        // Assert - all data should survive round-trip
        Assert.NotNull(deserialized);
        Assert.Equal(report.Status, deserialized.Status);
        Assert.Equal(report.Tools.Count, deserialized.Tools.Count);
        Assert.True(deserialized.Tools["dotnet"].Installed);
        Assert.Equal("10.0.0", deserialized.Tools["dotnet"].Version);
    }
}
