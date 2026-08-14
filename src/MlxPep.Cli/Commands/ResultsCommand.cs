namespace MlxPep.Cli.Commands;

using System.Text.Json;
using MlxPep.Core;

public class ResultsListCommand
{
    public async Task<CommandResult> ExecuteAsync(CommandContext context, bool includeIncomplete = false, string? modelId = null)
    {
        var store = new AssessmentRunStore();
        var runs = store.ListRuns(requireVerifiedComplete: !includeIncomplete, modelId: modelId);

        if (context.JsonOutput)
        {
            var result = new
            {
                command = "results list",
                status = "ok",
                results_root = store.GetResultsRootPath(),
                run_count = runs.Count,
                runs = runs.Select(run => new
                {
                    run_id = run.RunId,
                    model_id = run.ModelId,
                    suite = run.Suite,
                    status = run.Status,
                    created_at = run.CreatedAt,
                    verified_complete = run.IsVerifiedComplete,
                    profile_count = run.ProfileIds.Count
                })
            };

            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return CommandResult.Success(data: result);
        }

        Console.WriteLine(store.RenderRunListMarkdown(runs));
        return CommandResult.Success();
    }
}

public class ResultsShowCommand
{
    public async Task<CommandResult> ExecuteAsync(CommandContext context, string? runId = null, string? modelId = null, bool includeIncomplete = false)
    {
        var store = new AssessmentRunStore();
        var run = !string.IsNullOrWhiteSpace(runId)
            ? store.GetRun(runId)
            : store.GetLatestRun(modelId, requireVerifiedComplete: !includeIncomplete);

        if (run == null)
        {
            return CommandResult.Failure("No matching assessment run found.");
        }

        if (context.JsonOutput)
        {
            var result = new
            {
                command = "results show",
                status = "ok",
                run
            };

            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return CommandResult.Success(data: result);
        }

        Console.WriteLine(store.RenderRunSummaryMarkdown(run));
        return CommandResult.Success();
    }
}

public class ResultsExportCommand
{
    public async Task<CommandResult> ExecuteAsync(CommandContext context, string outputPath, string? runId = null, string? modelId = null, string format = "markdown", bool includeIncomplete = false)
    {
        var store = new AssessmentRunStore();
        var run = !string.IsNullOrWhiteSpace(runId)
            ? store.GetRun(runId)
            : store.GetLatestRun(modelId, requireVerifiedComplete: !includeIncomplete);

        if (run == null)
        {
            return CommandResult.Failure("No matching assessment run found.");
        }

        var normalizedFormat = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "markdown";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

        if (normalizedFormat == "json")
        {
            var payload = JsonSerializer.Serialize(new { run }, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputPath, payload);
        }
        else
        {
            await File.WriteAllTextAsync(outputPath, store.RenderRunSummaryMarkdown(run));
        }

        if (context.JsonOutput)
        {
            var result = new
            {
                command = "results export",
                status = "ok",
                run_id = run.RunId,
                output_path = outputPath,
                format = normalizedFormat
            };
            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return CommandResult.Success(data: result);
        }

        Console.WriteLine($"Saved {normalizedFormat} results to {outputPath}");
        return CommandResult.Success();
    }
}