namespace MlxPep.Cli.Commands;

using System.Diagnostics;
using System.Text.Json;
using MlxPep.Cli.Services;
using MlxPep.Core;

/// <summary>
/// Handler for `mlx-pep models` subcommands.
/// Manages model discovery and download from Hugging Face cache.
/// </summary>
public class ModelsListCommand
{
    private readonly IOmlxModelsService _modelsService;

    public ModelsListCommand(IOmlxModelsService? modelsService = null)
    {
        _modelsService = modelsService ?? new OmlxModelsService();
    }

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        using var progress = context.CreateProgressScope("models list", 2);
        try
        {
            Debug.WriteLine("[ModelsListCommand] Executing models list command");
            context.Verbose("ModelsListCommand", "Listing cached models from the shared Hugging Face cache.");
            progress.StartStep("read shared Hugging Face cache entries");
            var models = await _modelsService.ListCachedModelsAsync();
            progress.CompleteStep($"loaded {models.Count} cache entries");

            progress.StartStep("render models list output");
            if (context.JsonOutput)
            {
                context.Verbose("ModelsListCommand", "JSON output branch selected for models list.");
                var result = new
                {
                    command = "models list",
                    status = "ok",
                    model_count = models.Count,
                    models = models.Select(model => new
                    {
                        repo_id = model.RepoId,
                        revision = model.Revision,
                        size_bytes = model.SizeBytes,
                        size = model.GetSize(),
                        last_modified = model.LastModified
                    }).ToList()
                };
                Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                progress.CompleteStep("rendered models list JSON");
                return CommandResult.Success(data: result);
            }

            if (models.Count == 0)
            {
                Debug.WriteLine("[ModelsListCommand] No models were found in the shared Hugging Face cache");
                context.Verbose("ModelsListCommand", "No cached models were found; rendering empty text output.");
                Console.WriteLine("No models found in the shared Hugging Face cache.");
                progress.CompleteStep("rendered empty models list");
                return CommandResult.Success();
            }

            Debug.WriteLine($"[ModelsListCommand] Rendering {models.Count} shared-cache model entries");
            context.Verbose("ModelsListCommand", $"Rendering {models.Count} cached models in text format.");
            Console.WriteLine("Models in Hugging Face cache:");
            Console.WriteLine(new string('-', 110));
            Console.WriteLine($"{"Repo ID",-55} {"Revision",-14} {"Size",-10} {"Last Modified",-24}");
            Console.WriteLine(new string('-', 110));

            foreach (var model in models)
            {
                var revision = model.Revision.Length > 12 ? model.Revision[..12] : model.Revision;
                Console.WriteLine($"{model.RepoId,-55} {revision,-14} {model.GetSize(),-10} {model.LastModified:yyyy-MM-dd HH:mm:ss}");
            }

            progress.CompleteStep("rendered models list table");

            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ModelsListCommand] Failed to list models: {ex.Message}");
            context.Verbose("ModelsListCommand", $"Models list failed with {ex.GetType().Name}: {ex.Message}");
            return CommandResult.Failure($"Failed to list models: {ex.Message}");
        }
        finally
        {
            context.Verbose("ModelsListCommand", "Models list command finished execution path.");
        }
    }
}

public class ModelsGetCommand
{
    private readonly IOmlxModelsService _modelsService;

    public ModelsGetCommand(IOmlxModelsService? modelsService = null)
    {
        _modelsService = modelsService ?? new OmlxModelsService();
    }

    public async Task<CommandResult> ExecuteAsync(string hfId, CommandContext context, bool waitForCompletion = true, bool loadAfterDownload = false)
    {
        using var progress = context.CreateProgressScope("models get", waitForCompletion ? (loadAfterDownload ? 4 : 3) : 2);
        try
        {
            Debug.WriteLine($"[ModelsGetCommand] Executing models get for {hfId}. waitForCompletion={waitForCompletion}, loadAfterDownload={loadAfterDownload}");
            context.Verbose("ModelsGetCommand", $"Starting model download for '{hfId}' with waitForCompletion={waitForCompletion} and loadAfterDownload={loadAfterDownload}.");
            progress.StartStep("start oMLX download request");
            var download = await _modelsService.DownloadModelAsync(hfId, waitForCompletion, loadAfterDownload);
            progress.CompleteStep($"download request accepted with status {download.Status}");

            progress.StartStep("render download result");
            if (context.JsonOutput)
            {
                context.Verbose("ModelsGetCommand", "JSON output branch selected for models get.");
                var result = new
                {
                    command = "models get",
                    status = "ok",
                    repo_id = hfId,
                    wait_for_completion = waitForCompletion,
                    load_after_download = loadAfterDownload,
                    task_id = download.TaskId,
                    download_status = download.Status,
                    loaded_into_memory = download.LoadedIntoMemory,
                    model = download.ModelStatus == null
                        ? null
                        : new
                        {
                            model_id = download.ModelStatus.ModelId,
                            source_repo_id = download.ModelStatus.SourceRepoId,
                            model_path = download.ModelStatus.ModelPath,
                            loaded = download.ModelStatus.Loaded,
                            is_loading = download.ModelStatus.IsLoading,
                            source_type = download.ModelStatus.SourceType
                        },
                    detail = download.Detail
                };
                Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                progress.CompleteStep("rendered models get JSON");
                return CommandResult.Success(data: result);
            }

            if (!waitForCompletion)
            {
                Debug.WriteLine($"[ModelsGetCommand] Download for {hfId} started without waiting for completion");
                context.Verbose("ModelsGetCommand", "No-wait branch selected; returning once the task is visible to oMLX.");
                Console.WriteLine($"Started oMLX download for {hfId}. Task ID: {download.TaskId ?? "unknown"}");
                progress.CompleteStep("rendered no-wait download response");
                return CommandResult.Success();
            }

            progress.StartStep("finalize completed download result");
            if (download.LoadedIntoMemory)
            {
                Debug.WriteLine($"[ModelsGetCommand] Download and load completed for {hfId}");
                context.Verbose("ModelsGetCommand", $"Download and memory load both completed for '{hfId}'.");
                Console.WriteLine($"Downloaded and loaded {hfId} into oMLX as {download.ModelStatus?.ModelId ?? hfId}.");
            }
            else
            {
                Debug.WriteLine($"[ModelsGetCommand] Download completed for {hfId} without loading into memory");
                context.Verbose("ModelsGetCommand", $"Download completed for '{hfId}' without a load-after-download step.");
                Console.WriteLine($"Downloaded {hfId} into the shared oMLX model store.");
            }

            progress.CompleteStep("rendered completed download response");

            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ModelsGetCommand] Failed to get model {hfId}: {ex.Message}");
            context.Verbose("ModelsGetCommand", $"Model download failed with {ex.GetType().Name}: {ex.Message}");
            return CommandResult.Failure($"Failed to get model: {ex.Message}");
        }
        finally
        {
            context.Verbose("ModelsGetCommand", "Models get command finished execution path.");
        }
    }
}

public class ModelsStatusCommand
{
    private readonly IOmlxModelsService _modelsService;

    public ModelsStatusCommand(IOmlxModelsService? modelsService = null)
    {
        _modelsService = modelsService ?? new OmlxModelsService();
    }

    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        using var progress = context.CreateProgressScope("models status", 2);
        try
        {
            Debug.WriteLine("[ModelsStatusCommand] Executing models status command");
            context.Verbose("ModelsStatusCommand", "Collecting oMLX download task and model status snapshot.");
            progress.StartStep("fetch oMLX download and model status snapshot");
            var snapshot = await _modelsService.GetStatusAsync();
            progress.CompleteStep($"fetched {snapshot.DownloadTasks.Count} tasks and {snapshot.Models.Count} models");

            progress.StartStep("render models status output");
            if (context.JsonOutput)
            {
                context.Verbose("ModelsStatusCommand", "JSON output branch selected for models status.");
                var result = new
                {
                    command = "models status",
                    status = "ok",
                    active_task_count = snapshot.DownloadTasks.Count,
                    model_count = snapshot.Models.Count,
                    tasks = snapshot.DownloadTasks.Select(task => new
                    {
                        task_id = task.TaskId,
                        repo_id = task.RepoId,
                        status = task.Status,
                        progress = task.Progress,
                        detail = task.Detail,
                        is_terminal = task.IsTerminal
                    }).ToList(),
                    models = snapshot.Models.Select(model => new
                    {
                        model_id = model.ModelId,
                        source_repo_id = model.SourceRepoId,
                        model_path = model.ModelPath,
                        loaded = model.Loaded,
                        is_loading = model.IsLoading,
                        source_type = model.SourceType
                    }).ToList()
                };

                Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                progress.CompleteStep("rendered models status JSON");
                return CommandResult.Success(data: result);
            }

            Console.WriteLine("oMLX download tasks:");
            if (snapshot.DownloadTasks.Count == 0)
            {
                Debug.WriteLine("[ModelsStatusCommand] No HF download tasks are visible");
                context.Verbose("ModelsStatusCommand", "No visible download tasks were returned by oMLX.");
                Console.WriteLine("  none");
            }
            else
            {
                Debug.WriteLine($"[ModelsStatusCommand] Rendering {snapshot.DownloadTasks.Count} HF download tasks");
                context.Verbose("ModelsStatusCommand", $"Rendering {snapshot.DownloadTasks.Count} visible download tasks.");
                foreach (var task in snapshot.DownloadTasks)
                {
                    var progressText = task.Progress.HasValue ? $" ({task.Progress:0.##})" : string.Empty;
                    Console.WriteLine($"  {task.TaskId}: {task.RepoId} -> {task.Status}{progressText}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("oMLX models:");
            if (snapshot.Models.Count == 0)
            {
                Debug.WriteLine("[ModelsStatusCommand] No oMLX models are visible in status output");
                context.Verbose("ModelsStatusCommand", "No model status entries were returned by oMLX.");
                Console.WriteLine("  none");
            }
            else
            {
                Debug.WriteLine($"[ModelsStatusCommand] Rendering {snapshot.Models.Count} oMLX model status entries");
                context.Verbose("ModelsStatusCommand", $"Rendering {snapshot.Models.Count} model status entries.");
                foreach (var model in snapshot.Models.OrderBy(model => model.ModelId, StringComparer.OrdinalIgnoreCase))
                {
                    var state = model.Loaded ? "loaded" : model.IsLoading ? "loading" : "available";
                    var source = string.IsNullOrWhiteSpace(model.SourceRepoId) ? model.ModelPath : model.SourceRepoId;
                    Console.WriteLine($"  {model.ModelId}: {state} [{source}]");
                }
            }

            progress.CompleteStep("rendered models status text output");

            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ModelsStatusCommand] Failed to get model status: {ex.Message}");
            context.Verbose("ModelsStatusCommand", $"Models status failed with {ex.GetType().Name}: {ex.Message}");
            return CommandResult.Failure($"Failed to get model status: {ex.Message}");
        }
        finally
        {
            context.Verbose("ModelsStatusCommand", "Models status command finished execution path.");
        }
    }
}
