namespace MlxPep.Cli.Tests.Commands;

using System.Text.Json;
using MlxPep.Cli.Commands;
using MlxPep.Core.Profiling;

[Collection("Console")]
public class AssessCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WithJsonOutput_WhenProfilingRejectsPartialRun_WritesErrorPayload()
    {
        var command = new AssessCommand(
            profilingRunner: new FakeProfilingRunner(
                new InvalidOperationException("Model-assessor run ended with non-success status 'partial'")));
        var context = new CommandContext { JsonOutput = true };

        var (result, output) = await ModelsCommandTestHelpers.CaptureOutputAsync(
            () => command.ExecuteAsync("mlx-community/test-model", context: context));
        var json = JsonDocument.Parse(output);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("assess", json.RootElement.GetProperty("command").GetString());
        Assert.Equal("error", json.RootElement.GetProperty("status").GetString());
        Assert.Contains("partial", json.RootElement.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithVerboseAndProgress_WhenProfilingRejectsPartialRun_WritesExplicitRejection()
    {
        var command = new AssessCommand(
            profilingRunner: new FakeProfilingRunner(
                new InvalidOperationException("Model-assessor run ended with non-success status 'partial'")));
        var context = new CommandContext(jsonOutput: false, verboseOutput: true, progressOutput: true);

        var (result, errorOutput) = await ModelsCommandTestHelpers.CaptureErrorAsync(
            () => command.ExecuteAsync("mlx-community/test-model", context: context));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("partial", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[verbose][AssessCommand]", errorOutput);
        Assert.Contains("[progress][assess]", errorOutput);
        Assert.Contains("assess rejected", errorOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("partial", errorOutput, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeProfilingRunner : ProfilingRunner
    {
        private readonly Exception? _exception;

        public FakeProfilingRunner(Exception? exception = null)
        {
            _exception = exception;
        }

        public override Task<bool> IsAvailableAsync() => Task.FromResult(true);

        public override Task<AssessmentRunResult> RunProfilingAsync(
            string modelHfId,
            string? assistantModelId = null,
            string suite = "full",
            string? topologyManifestPath = null)
        {
            if (_exception != null)
            {
                throw _exception;
            }

            throw new NotSupportedException("This test double only exercises rejection paths.");
        }
    }
}
