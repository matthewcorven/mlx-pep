namespace MlxPep.Cli.Commands;

using System.Text.Json;
using MlxPep.Core;

public class ResultsListCommand
{
    public async Task<CommandResult> ExecuteAsync(CommandContext context, bool includeIncomplete = false, string? modelId = null)
    {
        context.Verbose("ResultsListCommand", $"Listing results with includeIncomplete={includeIncomplete} and modelId='{modelId ?? "<all>"}'.");
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
            context.Verbose("ResultsListCommand", $"Rendered results list JSON for {runs.Count} runs.");
            return CommandResult.Success(data: result);
        }

        Console.WriteLine(store.RenderRunListMarkdown(runs));
        context.Verbose("ResultsListCommand", $"Rendered text results list for {runs.Count} runs.");
        return CommandResult.Success();
    }
}

public class ResultsShowCommand
{
    public async Task<CommandResult> ExecuteAsync(CommandContext context, string? runId = null, string? modelId = null, bool includeIncomplete = false)
    {
        context.Verbose("ResultsShowCommand", $"Showing results for runId='{runId ?? "<latest>"}', modelId='{modelId ?? "<any>"}', includeIncomplete={includeIncomplete}.");
        var store = new AssessmentRunStore();
        var run = !string.IsNullOrWhiteSpace(runId)
            ? store.GetRun(runId)
            : store.GetLatestRun(modelId, requireVerifiedComplete: !includeIncomplete);

        if (run == null)
        {
            context.Verbose("ResultsShowCommand", "No matching run was found for results show.");
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
            context.Verbose("ResultsShowCommand", $"Rendered JSON for run '{run.RunId}'.");
            return CommandResult.Success(data: result);
        }

        Console.WriteLine(store.RenderRunSummaryMarkdown(run));
        context.Verbose("ResultsShowCommand", $"Rendered text summary for run '{run.RunId}'.");
        return CommandResult.Success();
    }
}

public class ResultsExportCommand
{
    public async Task<CommandResult> ExecuteAsync(CommandContext context, string outputPath, string? runId = null, string? modelId = null, string format = "markdown", bool includeIncomplete = false)
    {
        using var progress = context.CreateProgressScope("results export", 3);
        context.Verbose("ResultsExportCommand", $"Exporting results to '{outputPath}' with format '{format}', runId='{runId ?? "<latest>"}', modelId='{modelId ?? "<any>"}'.");
        var store = new AssessmentRunStore();
        progress.StartStep("select assessment run");
        var run = !string.IsNullOrWhiteSpace(runId)
            ? store.GetRun(runId)
            : store.GetLatestRun(modelId, requireVerifiedComplete: !includeIncomplete);

        if (run == null)
        {
            context.Verbose("ResultsExportCommand", "No matching run was found for export.");
            progress.CompleteStep("no matching run found");
            return CommandResult.Failure("No matching assessment run found.");
        }
        progress.CompleteStep($"selected run '{run.RunId}'");

        var normalizedFormat = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "markdown";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

        progress.StartStep("write export file");
        if (normalizedFormat == "json")
        {
            context.Verbose("ResultsExportCommand", "JSON export branch selected.");
            var payload = JsonSerializer.Serialize(new { run }, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(outputPath, payload);
        }
        else
        {
            context.Verbose("ResultsExportCommand", "Markdown export branch selected.");
            await File.WriteAllTextAsync(outputPath, store.RenderRunSummaryMarkdown(run));
        }
        progress.CompleteStep("export file written");

        progress.StartStep("render export result");
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
            progress.CompleteStep("rendered export result JSON");
            return CommandResult.Success(data: result);
        }

        Console.WriteLine($"Saved {normalizedFormat} results to {outputPath}");
        progress.CompleteStep("rendered export result text");
        return CommandResult.Success();
    }
}