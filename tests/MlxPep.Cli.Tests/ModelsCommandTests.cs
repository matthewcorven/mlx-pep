namespace MlxPep.Cli.Tests.Commands;

using System.Text.Json;
using MlxPep.Cli.Commands;
using MlxPep.Cli.Services;
using MlxPep.Core;

[CollectionDefinition("Console", DisableParallelization = true)]
public sealed class ConsoleCollectionDefinition
{
}

[Collection("Console")]
public class ModelsListCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WithCachedModels_RendersTable()
    {
        var service = new FakeModelsService
        {
            CachedModels =
            [
                new Model("mlx-community/test-model", "abcdef1234567890", 1024, new DateTime(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc))
            ]
        };
        var command = new ModelsListCommand(service);
        var context = new CommandContext { JsonOutput = false };

        var (result, output) = await ModelsCommandTestHelpers.CaptureOutputAsync(() => command.ExecuteAsync(context));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Models in Hugging Face cache:", output);
        Assert.Contains("mlx-community/test-model", output);
        Assert.Contains("1 KB", output);
    }

    [Fact]
    public async Task ExecuteAsync_WithJsonOutput_WritesModelPayload()
    {
        var service = new FakeModelsService
        {
            CachedModels =
            [
                new Model("mlx-community/test-model", "abcdef1234567890", 2048, new DateTime(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc))
            ]
        };
        var command = new ModelsListCommand(service);
        var context = new CommandContext { JsonOutput = true };

        var (result, output) = await ModelsCommandTestHelpers.CaptureOutputAsync(() => command.ExecuteAsync(context));
        var json = JsonDocument.Parse(output);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("models list", json.RootElement.GetProperty("command").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("model_count").GetInt32());
    }
}

[Collection("Console")]
public class ModelsGetCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WithWaitForCompletion_RendersCompletionMessage()
    {
        var service = new FakeModelsService
        {
            DownloadResult = new ModelDownloadResult(
                "mlx-community/test-model",
                "task-123",
                "completed",
                WaitedForCompletion: true,
                LoadedIntoMemory: false,
                ModelStatus: new OmlxModelStatus("test-model", "mlx-community/test-model", "/Users/core/.omlx/models/mlx-community/test-model", false, false, "hf"),
                Detail: null)
        };
        var command = new ModelsGetCommand(service);
        var context = new CommandContext { JsonOutput = false };

        var (result, output) = await ModelsCommandTestHelpers.CaptureOutputAsync(() => command.ExecuteAsync("mlx-community/test-model", context));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Downloaded mlx-community/test-model", output);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoWait_RendersStartMessage()
    {
        var service = new FakeModelsService
        {
            DownloadResult = new ModelDownloadResult(
                "mlx-community/test-model",
                "task-123",
                "started",
                WaitedForCompletion: false,
                LoadedIntoMemory: false,
                ModelStatus: null,
                Detail: null)
        };
        var command = new ModelsGetCommand(service);
        var context = new CommandContext { JsonOutput = false };

        var (result, output) = await ModelsCommandTestHelpers.CaptureOutputAsync(() => command.ExecuteAsync("mlx-community/test-model", context, waitForCompletion: false));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Started oMLX download", output);
        Assert.Contains("task-123", output);
    }

    [Fact]
    public async Task ExecuteAsync_WithJsonOutput_WritesDownloadPayload()
    {
        var service = new FakeModelsService
        {
            DownloadResult = new ModelDownloadResult(
                "mlx-community/test-model",
                "task-123",
                "completed",
                WaitedForCompletion: true,
                LoadedIntoMemory: true,
                ModelStatus: new OmlxModelStatus("test-model", "mlx-community/test-model", "/Users/core/.omlx/models/mlx-community/test-model", true, false, "hf"),
                Detail: "done")
        };
        var command = new ModelsGetCommand(service);
        var context = new CommandContext { JsonOutput = true };

        var (result, output) = await ModelsCommandTestHelpers.CaptureOutputAsync(() => command.ExecuteAsync("mlx-community/test-model", context, loadAfterDownload: true));
        var json = JsonDocument.Parse(output);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("task-123", json.RootElement.GetProperty("task_id").GetString());
        Assert.True(json.RootElement.GetProperty("loaded_into_memory").GetBoolean());
    }

    [Fact]
    public async Task ExecuteAsync_WithVerboseAndProgress_WritesChattyStderr()
    {
        var service = new FakeModelsService
        {
            DownloadResult = new ModelDownloadResult(
                "mlx-community/test-model",
                "task-123",
                "started",
                WaitedForCompletion: false,
                LoadedIntoMemory: false,
                ModelStatus: null,
                Detail: null)
        };
        var command = new ModelsGetCommand(service);
        var context = new CommandContext(jsonOutput: false, verboseOutput: true, progressOutput: true);

        var (result, errorOutput) = await ModelsCommandTestHelpers.CaptureErrorAsync(() => command.ExecuteAsync("mlx-community/test-model", context, waitForCompletion: false));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("[verbose][ModelsGetCommand]", errorOutput);
        Assert.Contains("[progress][models get]", errorOutput);
    }

    [Fact]
    public void CreateProgressScope_WithCallback_ReportsOverallAndChildPercentages()
    {
        var context = new CommandContext(progressOutput: true)
        {
            ProgressCallback = update =>
            {
                Assert.Equal("assess", update.Operation);
                Assert.True(update.StepNumber >= 1);
                Assert.True(update.TotalSteps >= 1);
                Assert.InRange(update.WorkPercent, 0, 100);
                Assert.InRange(update.OverallPercent, 0, 100);
                Assert.False(string.IsNullOrWhiteSpace(update.Detail));
            }
        };

        using var progress = context.CreateProgressScope("assess", 5);
        progress.StartStep("validate input and environment");
        progress.ReportWork(50, "halfway through validation");
        progress.CompleteStep("input validation complete");
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceThrows_ReturnsFailure()
    {
        var service = new FakeModelsService
        {
            DownloadException = new InvalidOperationException("boom")
        };
        var command = new ModelsGetCommand(service);
        var context = new CommandContext { JsonOutput = false };

        var result = await command.ExecuteAsync("mlx-community/test-model", context);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("boom", result.Message);
    }
}

[Collection("Console")]
public class ModelsStatusCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WithTextOutput_RendersTasksAndModels()
    {
        var service = new FakeModelsService
        {
            StatusSnapshot = new ModelsStatusSnapshot(
            [
                new ModelDownloadTask("task-123", "mlx-community/test-model", "running", 25, null, false)
            ],
            [
                new OmlxModelStatus("test-model", "mlx-community/test-model", "/Users/core/.omlx/models/mlx-community/test-model", false, true, "hf")
            ])
        };
        var command = new ModelsStatusCommand(service);
        var context = new CommandContext { JsonOutput = false };

        var (result, output) = await ModelsCommandTestHelpers.CaptureOutputAsync(() => command.ExecuteAsync(context));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("oMLX download tasks:", output);
        Assert.Contains("task-123", output);
        Assert.Contains("oMLX models:", output);
        Assert.Contains("loading", output);
    }

    [Fact]
    public async Task ExecuteAsync_WithJsonOutput_WritesSnapshotPayload()
    {
        var service = new FakeModelsService
        {
            StatusSnapshot = new ModelsStatusSnapshot(
            [
                new ModelDownloadTask("task-123", "mlx-community/test-model", "running", 25, null, false)
            ],
            [
                new OmlxModelStatus("test-model", "mlx-community/test-model", "/Users/core/.omlx/models/mlx-community/test-model", false, true, "hf")
            ])
        };
        var command = new ModelsStatusCommand(service);
        var context = new CommandContext { JsonOutput = true };

        var (result, output) = await ModelsCommandTestHelpers.CaptureOutputAsync(() => command.ExecuteAsync(context));
        var json = JsonDocument.Parse(output);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, json.RootElement.GetProperty("active_task_count").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("model_count").GetInt32());
    }
}

internal sealed class FakeModelsService : IOmlxModelsService
{
    public IReadOnlyList<Model> CachedModels { get; set; } = Array.Empty<Model>();
    public ModelDownloadResult DownloadResult { get; set; } = new("repo", null, "started", false, false, null, null);
    public ModelsStatusSnapshot StatusSnapshot { get; set; } = new(Array.Empty<ModelDownloadTask>(), Array.Empty<OmlxModelStatus>());
    public Exception? DownloadException { get; set; }

    public Task<IReadOnlyList<Model>> ListCachedModelsAsync()
    {
        return Task.FromResult(CachedModels);
    }

    public Task<ModelDownloadResult> DownloadModelAsync(string repoId, bool waitForCompletion = true, bool loadAfterDownload = false, CancellationToken cancellationToken = default)
    {
        if (DownloadException != null)
        {
            throw DownloadException;
        }

        return Task.FromResult(DownloadResult);
    }

    public Task<ModelsStatusSnapshot> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(StatusSnapshot);
    }
}

internal static class ModelsCommandTestHelpers
{
    public static async Task<(CommandResult Result, string Output)> CaptureOutputAsync(Func<Task<CommandResult>> action)
    {
        var oldOutput = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            var result = await action();
            return (result, writer.ToString());
        }
        finally
        {
            Console.SetOut(oldOutput);
        }
    }

    public static async Task<(CommandResult Result, string ErrorOutput)> CaptureErrorAsync(Func<Task<CommandResult>> action)
    {
        var oldError = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);

        try
        {
            var result = await action();
            return (result, writer.ToString());
        }
        finally
        {
            Console.SetError(oldError);
        }
    }
}