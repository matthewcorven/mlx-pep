namespace MlxPep.Cli.Services;

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MlxPep.Core;

public interface IOmlxModelsService
{
    Task<IReadOnlyList<Model>> ListCachedModelsAsync();
    Task<ModelDownloadResult> DownloadModelAsync(string repoId, bool waitForCompletion = true, bool loadAfterDownload = false, CancellationToken cancellationToken = default);
    Task<ModelsStatusSnapshot> GetStatusAsync(CancellationToken cancellationToken = default);
}

public sealed record ModelDownloadTask(
    string TaskId,
    string RepoId,
    string Status,
    double? Progress,
    string? Detail,
    bool IsTerminal);

public sealed record OmlxModelStatus(
    string ModelId,
    string? SourceRepoId,
    string ModelPath,
    bool Loaded,
    bool IsLoading,
    string? SourceType);

public sealed record ModelsStatusSnapshot(
    IReadOnlyList<ModelDownloadTask> DownloadTasks,
    IReadOnlyList<OmlxModelStatus> Models);

public sealed record ModelDownloadResult(
    string RepoId,
    string? TaskId,
    string Status,
    bool WaitedForCompletion,
    bool LoadedIntoMemory,
    OmlxModelStatus? ModelStatus,
    string? Detail);

public class OmlxModelsService : IOmlxModelsService
{
    private static readonly TimeSpan DownloadPollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(30);

    private readonly IHFCacheReader _cacheReader;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _publicClient;
    private readonly string _baseUrl;
    private readonly Func<string?> _apiKeyProvider;

    public OmlxModelsService(
        IHFCacheReader? cacheReader = null,
        HttpClient? adminClient = null,
        HttpClient? publicClient = null,
        string? baseUrl = null,
        Func<string?>? apiKeyProvider = null)
    {
        _cacheReader = cacheReader ?? new HFCacheReader();
        _baseUrl = ResolveBaseUrl(baseUrl);
        _apiKeyProvider = apiKeyProvider ?? (() => Environment.GetEnvironmentVariable("OMLX_API_KEY"));

        if (adminClient == null)
        {
            Debug.WriteLine("[OmlxModelsService] Creating default admin HTTP client with cookie container");
            var handler = new HttpClientHandler
            {
                CookieContainer = new CookieContainer()
            };
            _adminClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }
        else
        {
            Debug.WriteLine("[OmlxModelsService] Using injected admin HTTP client");
            _adminClient = adminClient;
        }

        if (publicClient == null)
        {
            Debug.WriteLine("[OmlxModelsService] Creating default public HTTP client");
            _publicClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }
        else
        {
            Debug.WriteLine("[OmlxModelsService] Using injected public HTTP client");
            _publicClient = publicClient;
        }
    }

    public async Task<IReadOnlyList<Model>> ListCachedModelsAsync()
    {
        Debug.WriteLine("[OmlxModelsService] Listing cached Hugging Face models");
        var models = await _cacheReader.ListModelsAsync();
        var orderedModels = models
            .OrderBy(model => model.RepoId, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(model => model.LastModified)
            .ToList();
        Debug.WriteLine($"[OmlxModelsService] Returning {orderedModels.Count} cached model entries");
        return orderedModels;
    }

    public async Task<ModelDownloadResult> DownloadModelAsync(string repoId, bool waitForCompletion = true, bool loadAfterDownload = false, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[OmlxModelsService] Starting download for {repoId}. waitForCompletion={waitForCompletion}, loadAfterDownload={loadAfterDownload}");
        await EnsureAdminSessionAsync(cancellationToken);

        string? taskId = await StartDownloadAsync(repoId, cancellationToken);
        if (string.IsNullOrWhiteSpace(taskId))
        {
            Debug.WriteLine($"[OmlxModelsService] Start response omitted a task id for {repoId}; resolving from the visible task list");
            taskId = await TryResolveVisibleTaskIdAsync(repoId, cancellationToken);
        }

        if (!waitForCompletion)
        {
            Debug.WriteLine($"[OmlxModelsService] Returning immediately after starting download for {repoId}");
            return new ModelDownloadResult(repoId, taskId, "started", WaitedForCompletion: false, LoadedIntoMemory: false, ModelStatus: null, Detail: null);
        }

        var task = await WaitForDownloadCompletionAsync(repoId, taskId, cancellationToken);
        if (!IsSuccessfulStatus(task.Status))
        {
            Debug.WriteLine($"[OmlxModelsService] Download for {repoId} ended unsuccessfully with status {task.Status}");
            throw new InvalidOperationException($"Download ended with status '{task.Status}'{FormatDetailSuffix(task.Detail)}");
        }

        var modelStatus = await FindModelStatusAsync(repoId, cancellationToken);
        if (loadAfterDownload)
        {
            if (modelStatus == null)
            {
                Debug.WriteLine($"[OmlxModelsService] Download for {repoId} completed but no oMLX model status entry was found");
                throw new InvalidOperationException($"Download completed, but oMLX did not report a matching model for '{repoId}'.");
            }

            await LoadModelAsync(modelStatus.ModelId, cancellationToken);
            modelStatus = await FindModelStatusAsync(repoId, cancellationToken);
        }
        else
        {
            Debug.WriteLine($"[OmlxModelsService] Download for {repoId} completed without requesting model load");
        }

        return new ModelDownloadResult(
            repoId,
            task.TaskId,
            task.Status,
            WaitedForCompletion: true,
            LoadedIntoMemory: modelStatus?.Loaded == true,
            ModelStatus: modelStatus,
            Detail: task.Detail);
    }

    public async Task<ModelsStatusSnapshot> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        Debug.WriteLine("[OmlxModelsService] Fetching oMLX download task and model status snapshot");
        await EnsureAdminSessionAsync(cancellationToken);
        var tasks = await ListDownloadTasksAsync(cancellationToken);
        var models = await ListModelStatusesAsync(cancellationToken);
        Debug.WriteLine($"[OmlxModelsService] Snapshot contains {tasks.Count} download tasks and {models.Count} models");
        return new ModelsStatusSnapshot(tasks, models);
    }

    private async Task EnsureAdminSessionAsync(CancellationToken cancellationToken)
    {
        var apiKey = ResolveApiKey();
        Debug.WriteLine("[OmlxModelsService] Logging into oMLX admin API");
        var payload = JsonSerializer.Serialize(new { api_key = apiKey });
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl("/admin/api/login"))
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var response = await _adminClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            Debug.WriteLine("[OmlxModelsService] Admin login succeeded");
            return;
        }

        Debug.WriteLine($"[OmlxModelsService] Admin login failed with status {(int)response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"oMLX admin login failed with status {(int)response.StatusCode}{FormatDetailSuffix(body)}");
    }

    private async Task<string?> StartDownloadAsync(string repoId, CancellationToken cancellationToken)
    {
        Debug.WriteLine($"[OmlxModelsService] Posting HF download request for {repoId}");
        var payload = JsonSerializer.Serialize(new { repo_id = repoId });
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl("/admin/api/hf/download"))
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var response = await _adminClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Debug.WriteLine($"[OmlxModelsService] HF download start failed for {repoId} with status {(int)response.StatusCode}");
            throw new InvalidOperationException($"Failed to start download for '{repoId}' with status {(int)response.StatusCode}{FormatDetailSuffix(body)}");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            Debug.WriteLine($"[OmlxModelsService] HF download start response for {repoId} was empty");
            return null;
        }

        using var document = JsonDocument.Parse(body);
        var taskId = ReadFirstString(document.RootElement, "task_id", "id");
        if (!string.IsNullOrWhiteSpace(taskId))
        {
            Debug.WriteLine($"[OmlxModelsService] HF download for {repoId} started with task id {taskId}");
        }
        else
        {
            Debug.WriteLine($"[OmlxModelsService] HF download for {repoId} started without a task id in the response");
        }

        return taskId;
    }

    private async Task<ModelDownloadTask> WaitForDownloadCompletionAsync(string repoId, string? taskId, CancellationToken cancellationToken)
    {
        Debug.WriteLine($"[OmlxModelsService] Waiting for download completion for {repoId}");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(DownloadTimeout);

        while (!timeoutCts.IsCancellationRequested)
        {
            var snapshot = await GetStatusAsync(timeoutCts.Token);
            var matchingTask = FindMatchingTask(snapshot.DownloadTasks, repoId, taskId);

            if (matchingTask != null)
            {
                Debug.WriteLine($"[OmlxModelsService] Observed download task {matchingTask.TaskId} for {repoId} in status {matchingTask.Status}");
                if (matchingTask.IsTerminal)
                {
                    Debug.WriteLine($"[OmlxModelsService] Download task {matchingTask.TaskId} for {repoId} reached terminal status {matchingTask.Status}");
                    return matchingTask;
                }
            }
            else
            {
                var downloadedModel = FindMatchingModel(snapshot.Models, repoId);
                if (downloadedModel != null)
                {
                    Debug.WriteLine($"[OmlxModelsService] No download task was visible for {repoId}, but oMLX reports the model on disk");
                    return new ModelDownloadTask(taskId ?? downloadedModel.ModelId, repoId, "completed", null, "Model is available in oMLX inventory.", true);
                }

                Debug.WriteLine($"[OmlxModelsService] Download task for {repoId} is not visible yet; polling again");
            }

            await Task.Delay(DownloadPollInterval, timeoutCts.Token);
        }

        Debug.WriteLine($"[OmlxModelsService] Timed out while waiting for download completion for {repoId}");
        throw new TimeoutException($"Timed out waiting for oMLX to finish downloading '{repoId}'.");
    }

    private async Task<IReadOnlyList<ModelDownloadTask>> ListDownloadTasksAsync(CancellationToken cancellationToken)
    {
        Debug.WriteLine("[OmlxModelsService] Listing oMLX HF download tasks");
        using var response = await _adminClient.GetAsync(BuildUrl("/admin/api/hf/tasks"), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Debug.WriteLine($"[OmlxModelsService] Listing HF tasks failed with status {(int)response.StatusCode}");
            throw new InvalidOperationException($"Failed to read oMLX download tasks with status {(int)response.StatusCode}{FormatDetailSuffix(body)}");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            Debug.WriteLine("[OmlxModelsService] HF task list response was empty");
            return Array.Empty<ModelDownloadTask>();
        }

        using var document = JsonDocument.Parse(body);
        return ParseDownloadTasks(document.RootElement);
    }

    private async Task<IReadOnlyList<OmlxModelStatus>> ListModelStatusesAsync(CancellationToken cancellationToken)
    {
        Debug.WriteLine("[OmlxModelsService] Listing public oMLX model status payload");
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl("/v1/models/status"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ResolveApiKey());

        using var response = await _publicClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Debug.WriteLine($"[OmlxModelsService] Listing model status failed with status {(int)response.StatusCode}");
            throw new InvalidOperationException($"Failed to read oMLX model status with status {(int)response.StatusCode}{FormatDetailSuffix(body)}");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            Debug.WriteLine("[OmlxModelsService] Model status response was empty");
            return Array.Empty<OmlxModelStatus>();
        }

        using var document = JsonDocument.Parse(body);
        var modelsElement = document.RootElement.TryGetProperty("models", out var foundModels) ? foundModels : document.RootElement;
        if (modelsElement.ValueKind != JsonValueKind.Array)
        {
            Debug.WriteLine("[OmlxModelsService] Model status payload did not contain a models array");
            return Array.Empty<OmlxModelStatus>();
        }

        var statuses = new List<OmlxModelStatus>();
        foreach (var modelElement in modelsElement.EnumerateArray())
        {
            var modelId = ReadFirstString(modelElement, "id") ?? "unknown";
            var sourceRepoId = ReadFirstString(modelElement, "source_repo_id");
            var modelPath = ReadFirstString(modelElement, "model_path") ?? string.Empty;
            var loaded = ReadBoolean(modelElement, "loaded");
            var isLoading = ReadBoolean(modelElement, "is_loading");
            var sourceType = ReadFirstString(modelElement, "source_type");
            statuses.Add(new OmlxModelStatus(modelId, sourceRepoId, modelPath, loaded, isLoading, sourceType));
        }

        Debug.WriteLine($"[OmlxModelsService] Parsed {statuses.Count} model status entries");
        return statuses;
    }

    private async Task<OmlxModelStatus?> FindModelStatusAsync(string repoId, CancellationToken cancellationToken)
    {
        Debug.WriteLine($"[OmlxModelsService] Resolving oMLX model status entry for {repoId}");
        var models = await ListModelStatusesAsync(cancellationToken);
        return FindMatchingModel(models, repoId);
    }

    private async Task LoadModelAsync(string modelId, CancellationToken cancellationToken)
    {
        Debug.WriteLine($"[OmlxModelsService] Loading oMLX model {modelId} into memory");
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl($"/v1/models/{Uri.EscapeDataString(modelId)}/load"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ResolveApiKey());
        using var response = await _publicClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            Debug.WriteLine($"[OmlxModelsService] Model {modelId} load completed successfully");
            return;
        }

        Debug.WriteLine($"[OmlxModelsService] Model {modelId} load failed with status {(int)response.StatusCode}");
        throw new InvalidOperationException($"Failed to load oMLX model '{modelId}' with status {(int)response.StatusCode}{FormatDetailSuffix(body)}");
    }

    private string ResolveApiKey()
    {
        var apiKey = _apiKeyProvider();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            Debug.WriteLine("[OmlxModelsService] Using OMLX_API_KEY from the environment or injected provider");
            return apiKey;
        }

        Debug.WriteLine("[OmlxModelsService] OMLX_API_KEY was not configured");
        throw new InvalidOperationException("OMLX_API_KEY is required for oMLX admin and public API access.");
    }

    private string BuildUrl(string relativePath)
    {
        return $"{_baseUrl}{relativePath}";
    }

    private static string ResolveBaseUrl(string? explicitBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(explicitBaseUrl))
        {
            Debug.WriteLine($"[OmlxModelsService] Using explicit oMLX base URL {explicitBaseUrl}");
            return explicitBaseUrl.TrimEnd('/');
        }

        var envBaseUrl = Environment.GetEnvironmentVariable("OMLX_BASE_URL");
        if (!string.IsNullOrWhiteSpace(envBaseUrl))
        {
            Debug.WriteLine($"[OmlxModelsService] Using OMLX_BASE_URL from environment: {envBaseUrl}");
            return envBaseUrl.TrimEnd('/');
        }

        Debug.WriteLine("[OmlxModelsService] OMLX_BASE_URL was not set; using default http://127.0.0.1:8000");
        return "http://127.0.0.1:8000";
    }

    private static IReadOnlyList<ModelDownloadTask> ParseDownloadTasks(JsonElement root)
    {
        var taskContainer = root.TryGetProperty("tasks", out var tasksProperty) ? tasksProperty : root;
        if (taskContainer.ValueKind != JsonValueKind.Array)
        {
            Debug.WriteLine("[OmlxModelsService] HF task payload did not contain an array");
            return Array.Empty<ModelDownloadTask>();
        }

        var tasks = new List<ModelDownloadTask>();
        foreach (var taskElement in taskContainer.EnumerateArray())
        {
            var taskId = ReadFirstString(taskElement, "task_id", "id") ?? Guid.NewGuid().ToString("N");
            var repoId = ReadFirstString(taskElement, "repo_id", "model_name", "name") ?? string.Empty;
            var status = ReadFirstString(taskElement, "status", "state", "task_status") ?? "unknown";
            var progress = ReadNullableDouble(taskElement, "progress", "progress_pct", "percent");
            var detail = ReadFirstString(taskElement, "detail", "message", "error");
            tasks.Add(new ModelDownloadTask(taskId, repoId, status, progress, detail, IsTerminalStatus(status)));
        }

        Debug.WriteLine($"[OmlxModelsService] Parsed {tasks.Count} HF task entries");
        return tasks;
    }

    private async Task<string?> TryResolveVisibleTaskIdAsync(string repoId, CancellationToken cancellationToken)
    {
        try
        {
            var tasks = await ListDownloadTasksAsync(cancellationToken);
            var matchingTask = FindMatchingTask(tasks, repoId, taskId: null);
            if (matchingTask != null)
            {
                Debug.WriteLine($"[OmlxModelsService] Resolved visible task id {matchingTask.TaskId} for {repoId}");
                return matchingTask.TaskId;
            }

            Debug.WriteLine($"[OmlxModelsService] No visible task id was available yet for {repoId}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OmlxModelsService] Failed to resolve a visible task id for {repoId}: {ex.Message}");
            return null;
        }
    }

    private static ModelDownloadTask? FindMatchingTask(IReadOnlyList<ModelDownloadTask> tasks, string repoId, string? taskId)
    {
        if (!string.IsNullOrWhiteSpace(taskId))
        {
            var taskById = tasks.FirstOrDefault(task => string.Equals(task.TaskId, taskId, StringComparison.OrdinalIgnoreCase));
            if (taskById != null)
            {
                Debug.WriteLine($"[OmlxModelsService] Matched download task by task id {taskId}");
                return taskById;
            }

            Debug.WriteLine($"[OmlxModelsService] No visible HF task matched task id {taskId}");
        }

        var taskByRepo = tasks.LastOrDefault(task => string.Equals(task.RepoId, repoId, StringComparison.OrdinalIgnoreCase));
        if (taskByRepo != null)
        {
            Debug.WriteLine($"[OmlxModelsService] Matched download task for repo {repoId}");
            return taskByRepo;
        }

        Debug.WriteLine($"[OmlxModelsService] No visible HF task matched repo {repoId}");
        return null;
    }

    private static OmlxModelStatus? FindMatchingModel(IReadOnlyList<OmlxModelStatus> models, string repoId)
    {
        foreach (var model in models)
        {
            if (string.Equals(model.SourceRepoId, repoId, StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"[OmlxModelsService] Matched model {model.ModelId} by source_repo_id for {repoId}");
                return model;
            }

            if (!string.IsNullOrWhiteSpace(model.ModelPath) && model.ModelPath.Replace('\\', '/').EndsWith($"/{repoId}", StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"[OmlxModelsService] Matched model {model.ModelId} by model_path suffix for {repoId}");
                return model;
            }
        }

        var requestedLeafName = repoId.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (string.IsNullOrWhiteSpace(requestedLeafName))
        {
            Debug.WriteLine($"[OmlxModelsService] Could not derive a leaf model name from repo id {repoId}");
            return null;
        }

        var modelByLeaf = models.FirstOrDefault(model => string.Equals(model.ModelId, requestedLeafName, StringComparison.OrdinalIgnoreCase));
        if (modelByLeaf != null)
        {
            Debug.WriteLine($"[OmlxModelsService] Matched model {modelByLeaf.ModelId} by leaf name {requestedLeafName}");
            return modelByLeaf;
        }

        Debug.WriteLine($"[OmlxModelsService] No oMLX model entry matched repo id {repoId}");
        return null;
    }

    private static string? ReadFirstString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }
        }

        return null;
    }

    private static bool ReadBoolean(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False))
        {
            return property.GetBoolean();
        }

        return false;
    }

    private static double? ReadNullableDouble(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var numberValue))
            {
                return numberValue;
            }

            if (property.ValueKind == JsonValueKind.String && double.TryParse(property.GetString(), out numberValue))
            {
                return numberValue;
            }
        }

        return null;
    }

    private static bool IsTerminalStatus(string status)
    {
        return status.Equals("completed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("complete", StringComparison.OrdinalIgnoreCase)
            || status.Equals("success", StringComparison.OrdinalIgnoreCase)
            || status.Equals("done", StringComparison.OrdinalIgnoreCase)
            || status.Equals("failed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("error", StringComparison.OrdinalIgnoreCase)
            || status.Equals("cancelled", StringComparison.OrdinalIgnoreCase)
            || status.Equals("canceled", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSuccessfulStatus(string status)
    {
        return status.Equals("completed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("complete", StringComparison.OrdinalIgnoreCase)
            || status.Equals("success", StringComparison.OrdinalIgnoreCase)
            || status.Equals("done", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatDetailSuffix(string? detail)
    {
        return string.IsNullOrWhiteSpace(detail) ? string.Empty : $": {detail.Trim()}";
    }
}