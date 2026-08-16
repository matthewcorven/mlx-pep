namespace MlxPep.Core.Tests.Profiling;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using MlxPep.Core.Profiling;

public class ProfilingRunnerTests
{
    private readonly ProfilingRunner _runner = new();

    [Fact]
    public async Task IsAvailableAsync_ReturnsTrueWhenAssessmentScriptsAreAvailable()
    {
        // Act
        var available = await _runner.IsAvailableAsync();

        // Assert - the adjacent model-assessor scripts should be discoverable in this repo.
        Assert.True(available);
    }

    [Fact]
    public async Task RunProfilingAsync_WithNullModelHfId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _runner.RunProfilingAsync(null!));
    }

    [Fact]
    public async Task RunProfilingAsync_WithEmptyModelHfId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _runner.RunProfilingAsync(string.Empty));
    }

    [Fact]
    public async Task RunProfilingAsync_WithWhitespaceModelHfId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _runner.RunProfilingAsync("   "));
    }

    [Fact]
    public async Task RunProfilingAsync_WithValidModelId_HandlesGracefully()
    {
        // When model-assessor is not available, should throw InvalidOperationException or timeout
        // (implementation doesn't validate suite parameter, so it just tries to run the subprocess)
        var exception = await Record.ExceptionAsync(
            () => _runner.RunProfilingAsync("test/model", null, "full"));

        // Should have some exception (either timeout or process failure)
        Assert.NotNull(exception);
    }

        [Fact]
        public void ReadSelectedProfileIds_WithMtpOff_ExcludesMtpEnabledProfiles()
        {
                var benchmarkProfilesJson = """
                {
                    "profiles": [
                        { "id": "short_code_research_tools_mtp_off", "workload": "short_code_research_tools", "settings": { "mtp_enabled": false, "vlm_mtp_enabled": false } },
                        { "id": "short_code_research_tools_mtp_on", "workload": "short_code_research_tools", "settings": { "mtp_enabled": true, "vlm_mtp_enabled": true } },
                        { "id": "short_coding_mtp_off", "workload": "short_coding", "settings": { "mtp_enabled": false, "vlm_mtp_enabled": false } },
                        { "id": "short_coding_mtp_on", "workload": "short_coding", "settings": { "mtp_enabled": true, "vlm_mtp_enabled": true } }
                    ]
                }
                """;

                var smokeSuiteJson = """
                {
                    "profiles": [
                        "short_code_research_tools_mtp_off",
                        "short_code_research_tools_mtp_on",
                        "short_coding_mtp_off",
                        "short_coding_mtp_on"
                    ]
                }
                """;

                using var benchmarkProfiles = JsonDocument.Parse(benchmarkProfilesJson);
                var smokeSuitePath = Path.GetTempFileName();
                File.WriteAllText(smokeSuitePath, smokeSuiteJson);

                try
                {
                        var method = typeof(ProfilingRunner).GetMethod(
                                "ReadSelectedProfileIds",
                                BindingFlags.NonPublic | BindingFlags.Static);

                        Assert.NotNull(method);

                        var result = (List<string>)method!.Invoke(null, new object[]
                        {
                                "smoke",
                                "off",
                                benchmarkProfiles.RootElement,
                                smokeSuitePath
                        })!;

                        Assert.Equal(new[]
                        {
                                "short_code_research_tools_mtp_off",
                                "short_coding_mtp_off"
                        }, result);
                }
                finally
                {
                        File.Delete(smokeSuitePath);
                }
        }

    [Fact]
    public void ParseRunManifest_WithPartialStatus_ThrowsInvalidOperationException()
    {
        var manifestJson = "{\"run_id\":\"run-123\",\"status\":\"partial\",\"model_id\":\"test/model\",\"suite\":\"full\",\"mtp_mode\":\"off\",\"created_at\":\"2026-08-14T00:00:00Z\"}";

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProfilingRunner.ParseRunManifest(manifestJson));

        Assert.Contains("partial", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRunManifest_WithFailedStatus_ThrowsInvalidOperationException()
    {
        var manifestJson = "{\"run_id\":\"run-123\",\"status\":\"failed\",\"model_id\":\"test/model\",\"suite\":\"full\",\"mtp_mode\":\"off\",\"created_at\":\"2026-08-14T00:00:00Z\"}";

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProfilingRunner.ParseRunManifest(manifestJson));

        Assert.Contains("failed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRunManifest_WithSuccessStatus_ReturnsRunInfo()
    {
        var manifestJson = "{\"run_id\":\"run-123\",\"status\":\"success\",\"model_id\":\"test/model\",\"suite\":\"full\",\"mtp_mode\":\"off\",\"created_at\":\"2026-08-14T00:00:00Z\"}";

        var runInfo = ProfilingRunner.ParseRunManifest(manifestJson);

        Assert.Equal("run-123", runInfo.RunId);
        Assert.Equal("success", runInfo.Status);
        Assert.True(runInfo.IsSuccess);
    }

    [Fact]
    public void ParseRunManifest_WithUppercaseSuccessStatus_ThrowsInvalidOperationException()
    {
        var manifestJson = "{\"run_id\":\"run-123\",\"status\":\"SUCCESS\",\"model_id\":\"test/model\",\"suite\":\"full\",\"mtp_mode\":\"off\",\"created_at\":\"2026-08-14T00:00:00Z\"}";

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProfilingRunner.ParseRunManifest(manifestJson));

        Assert.Contains("SUCCESS", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRunManifest_WithMissingRunId_ThrowsJsonException()
    {
        var manifestJson = "{\"status\":\"success\",\"model_id\":\"test/model\",\"suite\":\"full\",\"mtp_mode\":\"off\",\"created_at\":\"2026-08-14T00:00:00Z\"}";

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProfilingRunner.ParseRunManifest(manifestJson));

        Assert.Contains("run_id", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRunManifest_WithMissingStatus_ThrowsInvalidOperationException()
    {
        var manifestJson = "{\"run_id\":\"run-123\",\"model_id\":\"test/model\",\"suite\":\"full\",\"mtp_mode\":\"off\",\"created_at\":\"2026-08-14T00:00:00Z\"}";

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProfilingRunner.ParseRunManifest(manifestJson));

        Assert.Contains("status", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRunManifest_WithInvalidJson_ThrowsJsonException()
    {
        var manifestJson = "{invalid json";

        var threwException = false;
        var exceptionIsJson = false;
        try
        {
            ProfilingRunner.ParseRunManifest(manifestJson);
        }
        catch (Exception ex)
        {
            threwException = true;
            exceptionIsJson = ex.GetType().Name.Contains("Json");
        }

        Assert.True(threwException, "Expected an exception to be thrown");
        Assert.True(exceptionIsJson, "Expected a JSON-related exception");
    }

    [Fact]
    public void ParseRunManifest_WithNullStatus_ThrowsInvalidOperationException()
    {
        var manifestJson = "{\"run_id\":\"run-123\",\"status\":null,\"model_id\":\"test/model\",\"suite\":\"full\",\"mtp_mode\":\"off\",\"created_at\":\"2026-08-14T00:00:00Z\"}";

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProfilingRunner.ParseRunManifest(manifestJson));

        Assert.Contains("status", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRunManifest_WithEmptyString_ThrowsJsonException()
    {
        var manifestJson = "";

        var threwException = false;
        var exceptionIsJson = false;
        try
        {
            ProfilingRunner.ParseRunManifest(manifestJson);
        }
        catch (Exception ex)
        {
            threwException = true;
            exceptionIsJson = ex.GetType().Name.Contains("Json");
        }

        Assert.True(threwException, "Expected an exception to be thrown");
        Assert.True(exceptionIsJson, "Expected a JSON-related exception");
    }
}
