namespace MlxPep.Cli.Tests.Commands;

using System.Reflection;
using System.Threading;
using System.Text.Json;
using MlxPep.Cli.Commands;
using MlxPep.Core;
using MlxPep.Core.Profiling;

[Collection("Console")]
public class AssessCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WithJsonOutput_WhenProfilingRejectsPartialRun_WritesErrorPayload()
    {
        var command = new AssessCommand(
            profilingRunner: new FakeProfilingRunner(
                "Model-assessor run ended with non-success status 'partial'"));
        var context = new CommandContext { JsonOutput = true };

        var (result, output) = await ModelsCommandTestHelpers.CaptureOutputAsync(
            () => command.ExecuteAsync("mlx-community/test-model", publish: true, context: context));
        var json = JsonDocument.Parse(output);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("assess", json.RootElement.GetProperty("command").GetString());
        Assert.Equal("error", json.RootElement.GetProperty("status").GetString());
        Assert.False(json.RootElement.GetProperty("published").GetBoolean());
        Assert.Contains("partial", json.RootElement.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithVerboseAndProgress_WhenProfilingRejectsPartialRun_WritesExplicitRejection()
    {
        var command = new AssessCommand(
            profilingRunner: new FakeProfilingRunner(
                "Model-assessor run ended with non-success status 'partial'"));
        var context = new CommandContext(jsonOutput: false, verboseOutput: true, progressOutput: true);

        var (result, errorOutput) = await ModelsCommandTestHelpers.CaptureErrorAsync(
            () => command.ExecuteAsync("mlx-community/test-model", context: context));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("partial", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[verbose][AssessCommand]", errorOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[progress][assess]", errorOutput);
        Assert.Contains("assess rejected", errorOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("partial", errorOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithVerboseAndProgress_WhenProfilingSucceeds_StreamsSubprocessOutput()
    {
        var command = new AssessCommand(
            profilingRunner: new RealProcessProfilingRunner());
        var context = new CommandContext(jsonOutput: false, verboseOutput: true, progressOutput: true);

        var (result, output, errorOutput) = await CaptureConsoleAsync(
            () => command.ExecuteAsync("mlx-community/test-model", context: context));

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("streamed stdout", output, StringComparison.Ordinal);
        Assert.Contains("[progress][ProfilingRunner] Starting assessment workflow", errorOutput, StringComparison.Ordinal);
        Assert.Contains("[progress][ProfilingRunner] [stdout] streamed stdout", errorOutput, StringComparison.Ordinal);
        Assert.Contains("[progress][ProfilingRunner] [stderr] streamed stderr", errorOutput, StringComparison.Ordinal);
        Assert.Contains("[progress][assess]", errorOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithProgressOnly_WhenProfilingSucceeds_StreamsSubprocessOutput()
    {
        var command = new AssessCommand(
            profilingRunner: new RealProcessProfilingRunner());
        var context = new CommandContext(jsonOutput: false, verboseOutput: false, progressOutput: true);

        var (result, output, errorOutput) = await CaptureConsoleAsync(
            () => command.ExecuteAsync("mlx-community/test-model", context: context));

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("streamed stdout", output, StringComparison.Ordinal);
        Assert.Contains("[progress][ProfilingRunner] [stdout] streamed stdout", errorOutput, StringComparison.Ordinal);
        Assert.Contains("[progress][ProfilingRunner] [stderr] streamed stderr", errorOutput, StringComparison.Ordinal);
        Assert.Contains("[progress][assess]", errorOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithJsonOutput_WhenProfilingSucceeds_SuppressesSubprocessStreaming()
    {
        var command = new AssessCommand(
            profilingRunner: new FakeProfilingRunner(streamedStdout: "streamed stdout", streamedStderr: "streamed stderr"));
        var context = new CommandContext(jsonOutput: true, verboseOutput: true, progressOutput: true);

        var (result, output, errorOutput) = await CaptureConsoleAsync(
            () => command.ExecuteAsync("mlx-community/test-model", context: context));

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("streamed stdout", output, StringComparison.Ordinal);
        Assert.DoesNotContain("streamed stderr", errorOutput, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"ok\"", output, StringComparison.Ordinal);
    }

    private static async Task<(CommandResult Result, string Output, string ErrorOutput)> CaptureConsoleAsync(Func<Task<CommandResult>> action)
    {
        var oldOutput = Console.Out;
        var oldError = Console.Error;
        using var outputWriter = new StringWriter();
        using var errorWriter = new StringWriter();
        Console.SetOut(outputWriter);
        Console.SetError(errorWriter);

        try
        {
            var result = await action();
            return (result, outputWriter.ToString(), errorWriter.ToString());
        }
        finally
        {
            Console.SetOut(oldOutput);
            Console.SetError(oldError);
        }
    }

    private sealed class FakeProfilingRunner : ProfilingRunner
    {
        private readonly string? _exceptionMessage;
        private readonly string? _streamedStdout;
        private readonly string? _streamedStderr;

        public FakeProfilingRunner(string? exceptionMessage = null, string? streamedStdout = null, string? streamedStderr = null)
        {
            _exceptionMessage = exceptionMessage;
            _streamedStdout = streamedStdout;
            _streamedStderr = streamedStderr;
        }

        public override Task<bool> IsAvailableAsync() => Task.FromResult(true);

        public override Task<AssessmentRunResult> RunProfilingAsync(
            string modelHfId,
            string? assistantModelId = null,
            string suite = "full",
            string? topologyManifestPath = null,
            Action<string, bool>? outputHandler = null)
        {
            if (_exceptionMessage != null)
            {
                throw new InvalidOperationException(_exceptionMessage);
            }

            outputHandler?.Invoke(_streamedStdout ?? "profiling stdout", false);
            outputHandler?.Invoke(_streamedStderr ?? "profiling stderr", true);

            var manifest = new RecommendationManifest(
                ModelHfId: modelHfId,
                AssessmentVersion: "test",
                Timestamp: DateTime.UtcNow.ToString("O"),
                Recommendations: new Dictionary<string, TierRecommendation>(StringComparer.OrdinalIgnoreCase)
                {
                    ["high"] = new(
                        Tier: "high",
                        System: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
                        Omlx: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
                        Harness: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase))
                });

            return Task.FromResult(new AssessmentRunResult(
                OperationId: "operation-123",
                RunId: "run-123",
                ModelId: modelHfId,
                Status: "success",
                Suite: suite,
                MtpMode: "off",
                CreatedAt: "2026-08-18T00:00:00Z",
                RecommendationManifest: manifest));
        }
    }

    private sealed class RealProcessProfilingRunner : ProfilingRunner
    {
        public override Task<bool> IsAvailableAsync() => Task.FromResult(true);

        public override async Task<AssessmentRunResult> RunProfilingAsync(
            string modelHfId,
            string? assistantModelId = null,
            string suite = "full",
            string? topologyManifestPath = null,
            Action<string, bool>? outputHandler = null)
        {
            outputHandler?.Invoke($"Starting assessment workflow for model '{modelHfId}' in suite '{suite}'.", false);

            const string pythonScript =
                "import sys, time; print('streamed stdout', flush=True); print('streamed stderr', file=sys.stderr, flush=True); time.sleep(0.1)";
            var arguments = $"-c \"{pythonScript}\"";

            outputHandler?.Invoke($"Launching assessment subprocess: python3 {arguments}", false);
            await InvokeRunProcessAsync(arguments, outputHandler);

            var manifest = new RecommendationManifest(
                ModelHfId: modelHfId,
                AssessmentVersion: "test",
                Timestamp: DateTime.UtcNow.ToString("O"),
                Recommendations: new Dictionary<string, TierRecommendation>(StringComparer.OrdinalIgnoreCase)
                {
                    ["high"] = new(
                        Tier: "high",
                        System: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
                        Omlx: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
                        Harness: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase))
                });

            return new AssessmentRunResult(
                OperationId: "operation-123",
                RunId: "run-123",
                ModelId: modelHfId,
                Status: "success",
                Suite: suite,
                MtpMode: "off",
                CreatedAt: "2026-08-18T00:00:00Z",
                RecommendationManifest: manifest);
        }

        private async Task InvokeRunProcessAsync(string arguments, Action<string, bool>? outputHandler)
        {
            var method = typeof(ProfilingRunner).GetMethod("RunProcessAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var task = (Task)method!.Invoke(this, new object?[] { "python3", arguments, CancellationToken.None, outputHandler })!;
            await task;

            var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
            var exitCode = (int)result.GetType().GetField("Item1")!.GetValue(result)!;
            Assert.Equal(0, exitCode);
        }
    }
}
