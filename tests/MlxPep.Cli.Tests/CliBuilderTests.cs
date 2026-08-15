namespace MlxPep.Cli.Tests;

using System.Text.Json;
using MlxPep.Cli;
using MlxPep.Cli.Commands;
using MlxPep.Cli.Services;
using MlxPep.Core;

[Collection("Console")]
public class CliBuilderTests
{
    [Fact]
    public void ParseInvocation_StripsGlobalFlags_AndBuildsSharedContext()
    {
        var invocation = CliRuntime.ParseInvocation([
            "--verbose",
            "models",
            "list",
            "--json",
            "--progress"
        ]);

        Assert.Equal(["models", "list"], invocation.CommandArgs);
        Assert.True(invocation.Context.JsonOutput);
        Assert.True(invocation.Context.VerboseOutput);
        Assert.True(invocation.Context.ProgressOutput);
    }

    [Fact]
    public async Task RunAsync_ModelsList_WithStaticFakeService_RendersDeterministicHappyPath()
    {
        var originalFactory = CliBuilder.ModelsServiceFactory;
        CliBuilder.ModelsServiceFactory = () => new StaticFakeModelsService();

        try
        {
            var (exitCode, text) = await CaptureConsoleOutputAsync(() => CliBuilder.RunAsync(["models", "list"]));

            Assert.Equal(0, exitCode);
            Assert.Contains("Models in Hugging Face cache:", text);
            Assert.Contains("mlx-community/static-test-model", text);
            Assert.Contains("1 KB", text);
        }
        finally
        {
            CliBuilder.ModelsServiceFactory = originalFactory;
        }
    }

    [Fact]
    public async Task RunAsync_ModelsGet_NoWait_WithStaticFakeService_UsesCommandSurfaceAndReturnsSuccess()
    {
        var originalFactory = CliBuilder.ModelsServiceFactory;
        CliBuilder.ModelsServiceFactory = () => new StaticFakeModelsService();

        try
        {
            var (exitCode, text) = await CaptureConsoleOutputAsync(() => CliBuilder.RunAsync(["models", "get", "mlx-community/static-test-model", "--no-wait"]));

            Assert.Equal(0, exitCode);
            Assert.Contains("Started oMLX download", text);
            Assert.Contains("task-static-1", text);
        }
        finally
        {
            CliBuilder.ModelsServiceFactory = originalFactory;
        }
    }

    [Fact]
    public async Task RunAsync_ModelsStatus_WithStaticFakeService_RendersStableStatusSurface()
    {
        var originalFactory = CliBuilder.ModelsServiceFactory;
        CliBuilder.ModelsServiceFactory = () => new StaticFakeModelsService();

        try
        {
            var (exitCode, text) = await CaptureConsoleOutputAsync(() => CliBuilder.RunAsync(["models", "status"]));

            Assert.Equal(0, exitCode);
            Assert.Contains("oMLX download tasks:", text);
            Assert.Contains("task-static-1", text);
            Assert.Contains("oMLX models:", text);
            Assert.Contains("static-model", text);
        }
        finally
        {
            CliBuilder.ModelsServiceFactory = originalFactory;
        }
    }

    [Fact]
    public async Task RunAsync_ModelsGet_WhenFakeServiceThrows_ExitsNonZeroWithMeaningfulMessage()
    {
        var originalFactory = CliBuilder.ModelsServiceFactory;
        CliBuilder.ModelsServiceFactory = () => new StaticFakeModelsService { ThrowOnDownload = true };

        try
        {
            var (exitCode, text) = await CaptureConsoleOutputAsync(() => CliBuilder.RunAsync(["models", "get", "mlx-community/failing-model"]));

            Assert.Equal(1, exitCode);
            Assert.Contains("Failed to get model", text);
            Assert.Contains("download failure", text);
        }
        finally
        {
            CliBuilder.ModelsServiceFactory = originalFactory;
        }
    }

    private static async Task<(int ExitCode, string OutputText)> CaptureConsoleOutputAsync(Func<Task<int>> action)
    {
        var oldOut = Console.Out;
        var oldError = Console.Error;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        Console.SetError(writer);

        try
        {
            var exitCode = await action();
            return (exitCode, writer.ToString());
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetError(oldError);
        }
    }

    private sealed class StaticFakeModelsService : IOmlxModelsService
    {
        public bool ThrowOnDownload { get; set; }

        public Task<IReadOnlyList<Model>> ListCachedModelsAsync()
        {
            return Task.FromResult<IReadOnlyList<Model>>([
                new Model("mlx-community/static-test-model", "abcdef1234567890", 1024, new DateTime(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc))
            ]);
        }

        public Task<ModelDownloadResult> DownloadModelAsync(string repoId, bool waitForCompletion = true, bool loadAfterDownload = false, CancellationToken cancellationToken = default)
        {
            if (ThrowOnDownload)
            {
                throw new InvalidOperationException("download failure");
            }

            return Task.FromResult(new ModelDownloadResult(
                repoId,
                "task-static-1",
                waitForCompletion ? "completed" : "started",
                waitForCompletion,
                loadAfterDownload,
                new OmlxModelStatus("static-model", repoId, "/tmp/static-model", loadAfterDownload, false, "hf"),
                null));
        }

        public Task<ModelsStatusSnapshot> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ModelsStatusSnapshot(
            [
                new ModelDownloadTask("task-static-1", "mlx-community/static-test-model", "completed", 100, null, true)
            ],
            [
                new OmlxModelStatus("static-model", "mlx-community/static-test-model", "/tmp/static-model", true, false, "hf")
            ]));
        }
    }
}