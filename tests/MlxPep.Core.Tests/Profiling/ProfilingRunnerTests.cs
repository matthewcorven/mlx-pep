namespace MlxPep.Core.Tests.Profiling;

using System.Diagnostics;
using System;
using System.Threading.Tasks;
using Xunit;
using MlxPep.Core.Profiling;

[CollectionDefinition("DebugTrace", DisableParallelization = true)]
public sealed class DebugTraceCollectionDefinition
{
}

[Collection("DebugTrace")]
public class ProfilingRunnerTests
{
    private static readonly object TraceSyncRoot = new();
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
    public void ValidateBenchmarkResults_WithPartialBenchmarkStatus_ThrowsInvalidOperationException()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"profiling-runner-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempRoot, "runs", "run-123", "profile-a"));

        try
        {
            var benchmarkRelativePath = "runs/run-123/profile-a/06_bench_results.json";
            var benchmarkAbsolutePath = Path.Combine(tempRoot, "runs", "run-123", "profile-a", "06_bench_results.json");
            File.WriteAllText(benchmarkAbsolutePath, "{\"status\":\"partial\",\"results\":[]}");
            var manifestJson = $$"""
                {
                  "run_id":"run-123",
                  "status":"success",
                  "model_id":"test/model",
                  "suite":"full",
                  "mtp_mode":"off",
                  "created_at":"2026-08-14T00:00:00Z",
                  "artifact_paths":{
                    "benchmark_results":["{{benchmarkRelativePath}}"]
                  }
                }
                """;

            var output = CaptureDebugOutput(() =>
            {
                var exception = Assert.Throws<InvalidOperationException>(
                    () => ProfilingRunner.ValidateBenchmarkResults(tempRoot, manifestJson));

                Assert.Contains("partial", exception.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("06_bench_results.json", exception.Message, StringComparison.OrdinalIgnoreCase);
            });

            Assert.Contains("Rejecting benchmark result", output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Rejecting assessment because benchmark results were incomplete", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ValidateBenchmarkResults_WithCompletedBenchmarkStatus_DoesNotThrow()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"profiling-runner-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempRoot, "runs", "run-123", "profile-a"));

        try
        {
            var benchmarkRelativePath = "runs/run-123/profile-a/06_bench_results.json";
            var benchmarkAbsolutePath = Path.Combine(tempRoot, "runs", "run-123", "profile-a", "06_bench_results.json");
            File.WriteAllText(benchmarkAbsolutePath, "{\"status\":\"completed\",\"results\":[]}");
            var manifestJson = $$"""
                {
                  "run_id":"run-123",
                  "status":"success",
                  "model_id":"test/model",
                  "suite":"full",
                  "mtp_mode":"off",
                  "created_at":"2026-08-14T00:00:00Z",
                  "artifact_paths":{
                    "benchmark_results":["{{benchmarkRelativePath}}"]
                  }
                }
                """;

            var output = CaptureDebugOutput(() =>
            {
                var exception = Record.Exception(
                    () => ProfilingRunner.ValidateBenchmarkResults(tempRoot, manifestJson));

                Assert.Null(exception);
            });

            Assert.Contains("Accepted benchmark result", output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("All benchmark result artifacts completed successfully", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ValidateBenchmarkResults_WithMissingBenchmarkResults_DoesNotThrow()
    {
        const string manifestJson = """
            {
              "run_id":"run-123",
              "status":"success",
              "model_id":"test/model",
              "suite":"full",
              "mtp_mode":"off",
              "created_at":"2026-08-14T00:00:00Z",
              "artifact_paths":{}
            }
            """;

        var output = CaptureDebugOutput(() =>
        {
            var exception = Record.Exception(
                () => ProfilingRunner.ValidateBenchmarkResults(Path.GetTempPath(), manifestJson));

            Assert.Null(exception);
        });

        Assert.Contains("no benchmark results to validate", output, StringComparison.OrdinalIgnoreCase);
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

    private static string CaptureDebugOutput(Action action)
    {
        lock (TraceSyncRoot)
        {
            using var writer = new StringWriter();
            using var listener = new TextWriterTraceListener(writer);
            var originalAutoFlush = Trace.AutoFlush;
            Trace.Listeners.Add(listener);
            Trace.AutoFlush = true;

            try
            {
                action();
                return writer.ToString();
            }
            finally
            {
                Trace.AutoFlush = originalAutoFlush;
                listener.Flush();
                Trace.Listeners.Remove(listener);
            }
        }
    }
}
