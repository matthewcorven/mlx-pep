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
        try
        {
            Debug.WriteLine("[ModelsListCommand] Executing models list command");
            var models = await _modelsService.ListCachedModelsAsync();

            if (context.JsonOutput)
            {
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
                return CommandResult.Success(data: result);
            }

            if (models.Count == 0)
            {
                Debug.WriteLine("[ModelsListCommand] No models were found in the shared Hugging Face cache");
                Console.WriteLine("No models found in the shared Hugging Face cache.");
                return CommandResult.Success();
            }

            Debug.WriteLine($"[ModelsListCommand] Rendering {models.Count} shared-cache model entries");
            Console.WriteLine("Models in Hugging Face cache:");
            Console.WriteLine(new string('-', 110));
            Console.WriteLine($"{"Repo ID",-55} {"Revision",-14} {"Size",-10} {"Last Modified",-24}");
            Console.WriteLine(new string('-', 110));

            foreach (var model in models)
            {
                var revision = model.Revision.Length > 12 ? model.Revision[..12] : model.Revision;
                Console.WriteLine($"{model.RepoId,-55} {revision,-14} {model.GetSize(),-10} {model.LastModified:yyyy-MM-dd HH:mm:ss}");
            }

            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ModelsListCommand] Failed to list models: {ex.Message}");
            return CommandResult.Failure($"Failed to list models: {ex.Message}");
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
        try
        {
            Debug.WriteLine($"[ModelsGetCommand] Executing models get for {hfId}. waitForCompletion={waitForCompletion}, loadAfterDownload={loadAfterDownload}");
            var download = await _modelsService.DownloadModelAsync(hfId, waitForCompletion, loadAfterDownload);

            if (context.JsonOutput)
            {
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
                return CommandResult.Success(data: result);
            }

            if (!waitForCompletion)
            {
                Debug.WriteLine($"[ModelsGetCommand] Download for {hfId} started without waiting for completion");
                Console.WriteLine($"Started oMLX download for {hfId}. Task ID: {download.TaskId ?? "unknown"}");
                return CommandResult.Success();
            }

            if (download.LoadedIntoMemory)
            {
                Debug.WriteLine($"[ModelsGetCommand] Download and load completed for {hfId}");
                Console.WriteLine($"Downloaded and loaded {hfId} into oMLX as {download.ModelStatus?.ModelId ?? hfId}.");
            }
            else
            {
                Debug.WriteLine($"[ModelsGetCommand] Download completed for {hfId} without loading into memory");
                Console.WriteLine($"Downloaded {hfId} into the shared oMLX model store.");
            }

            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ModelsGetCommand] Failed to get model {hfId}: {ex.Message}");
            return CommandResult.Failure($"Failed to get model: {ex.Message}");
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
        try
        {
            Debug.WriteLine("[ModelsStatusCommand] Executing models status command");
            var snapshot = await _modelsService.GetStatusAsync();

            if (context.JsonOutput)
            {
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
                return CommandResult.Success(data: result);
            }

            Console.WriteLine("oMLX download tasks:");
            if (snapshot.DownloadTasks.Count == 0)
            {
                Debug.WriteLine("[ModelsStatusCommand] No HF download tasks are visible");
                Console.WriteLine("  none");
            }
            else
            {
                Debug.WriteLine($"[ModelsStatusCommand] Rendering {snapshot.DownloadTasks.Count} HF download tasks");
                foreach (var task in snapshot.DownloadTasks)
                {
                    var progress = task.Progress.HasValue ? $" ({task.Progress:0.##})" : string.Empty;
                    Console.WriteLine($"  {task.TaskId}: {task.RepoId} -> {task.Status}{progress}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("oMLX models:");
            if (snapshot.Models.Count == 0)
            {
                Debug.WriteLine("[ModelsStatusCommand] No oMLX models are visible in status output");
                Console.WriteLine("  none");
            }
            else
            {
                Debug.WriteLine($"[ModelsStatusCommand] Rendering {snapshot.Models.Count} oMLX model status entries");
                foreach (var model in snapshot.Models.OrderBy(model => model.ModelId, StringComparer.OrdinalIgnoreCase))
                {
                    var state = model.Loaded ? "loaded" : model.IsLoading ? "loading" : "available";
                    var source = string.IsNullOrWhiteSpace(model.SourceRepoId) ? model.ModelPath : model.SourceRepoId;
                    Console.WriteLine($"  {model.ModelId}: {state} [{source}]");
                }
            }

            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ModelsStatusCommand] Failed to get model status: {ex.Message}");
            return CommandResult.Failure($"Failed to get model status: {ex.Message}");
        }
    }
}
