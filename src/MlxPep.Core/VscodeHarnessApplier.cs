namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

/// <summary>
/// Applies profile harness blocks to VS Code and VS Code Insiders.
/// Issue #16: harness apply profile to Copilot CLI + VS Code/Insiders
/// </summary>
public class VscodeHarnessApplier : IHarnessApplier
{
    public string HarnessName => "vscode";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<HarnessApplyResult> ApplyAsync(
        Profile profile,
        bool isDryRun = false,
        bool requestedInsiders = false)
    {
        try
        {
            if (profile.Harness == null || !profile.Harness.ContainsKey("vscode"))
            {
                return new HarnessApplyResult(
                    ProfileId: profile.Id,
                    Harness: HarnessName,
                    IsDryRun: isDryRun,
                    Success: false,
                    Error: $"Profile does not contain harness.vscode block",
                    Changes: new());
            }

            var vscodeConfigObj = profile.Harness["vscode"];
            var vscodeConfig = ConvertToDict(vscodeConfigObj);

            if (vscodeConfig == null || vscodeConfig.Count == 0)
            {
                return new HarnessApplyResult(
                    ProfileId: profile.Id,
                    Harness: HarnessName,
                    IsDryRun: isDryRun,
                    Success: false,
                    Error: "harness.vscode is not a valid object",
                    Changes: new());
            }

            var changes = new List<FileChangeResult>();
            var vscodeDir = requestedInsiders
                ? GetVscodeInsidersUserDir()
                : GetVscodeUserDir();

            // Allow dry-run even if directory doesn't exist (for testing), but fail for real applies
            if (string.IsNullOrEmpty(vscodeDir) && !isDryRun)
            {
                return new HarnessApplyResult(
                    ProfileId: profile.Id,
                    Harness: HarnessName,
                    IsDryRun: isDryRun,
                    Success: false,
                    Error: $"VS Code {(requestedInsiders ? "Insiders " : "")}user config directory not found",
                    Changes: new());
            }

            // For dry-run without directory, use a hypothetical path
            vscodeDir ??= GetHypotheticalVscodeDir(requestedInsiders);

            // Process settings.json if customSettings present
            if (vscodeConfig.ContainsKey("customSettings"))
            {
                try
                {
                    var customSettingsObj = vscodeConfig["customSettings"];
                    var customSettings = ConvertToDict(customSettingsObj);

                    if (customSettings != null && customSettings.Count > 0)
                    {
                        var settingsChange = await ProcessSettingsJsonAsync(vscodeDir, customSettings);
                        changes.Add(settingsChange);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[ERROR in customSettings] {ex.Message}");
                    throw;
                }
            }

            // Process chatLanguageModels.json if present
            if (vscodeConfig.ContainsKey("chatLanguageModels"))
            {
                try
                {
                    var modelsObj = vscodeConfig["chatLanguageModels"];
                    var models = ConvertToDict(modelsObj);

                    if (models != null && models.Count > 0)
                    {
                        var modelsChange = await ProcessChatModelsJsonAsync(vscodeDir, models);
                        changes.Add(modelsChange);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[ERROR in chatLanguageModels] {ex.Message}");
                    throw;
                }
            }


            if (!isDryRun && changes.Any(c => c.Status != "unchanged"))
            {
                var backupMgr = new BackupManager();
                var (backupSuccess, backupLocation, backupError) = await backupMgr.CreateBackupAsync(HarnessName, profile.Id, changes);

                if (!backupSuccess)
                {
                    return new HarnessApplyResult(
                        ProfileId: profile.Id,
                        Harness: HarnessName,
                        IsDryRun: isDryRun,
                        Success: false,
                        Error: backupError,
                        Changes: changes);
                }

                // Write the changes
                foreach (var change in changes.Where(c => c.Status != "unchanged" && c.ProposedContent != null))
                {
                    try
                    {
                        var expandedPath = HarnessUtilities.ExpandPath(change.FilePath);
                        var directory = Path.GetDirectoryName(expandedPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        await File.WriteAllTextAsync(expandedPath, change.ProposedContent);
                    }
                    catch (Exception ex)
                    {
                        return new HarnessApplyResult(
                            ProfileId: profile.Id,
                            Harness: HarnessName,
                            IsDryRun: isDryRun,
                            Success: false,
                            Error: $"Failed to write {change.FilePath}: {ex.Message}",
                            Changes: changes,
                            BackupLocation: backupLocation);
                    }
                }

                return new HarnessApplyResult(
                    ProfileId: profile.Id,
                    Harness: HarnessName,
                    IsDryRun: isDryRun,
                    Success: true,
                    Changes: changes,
                    BackupLocation: backupLocation);
            }

            return new HarnessApplyResult(
                ProfileId: profile.Id,
                Harness: HarnessName,
                IsDryRun: isDryRun,
                Success: true,
                Changes: changes,
                BackupLocation: null);
        }
        catch (Exception ex)
        {
            return new HarnessApplyResult(
                ProfileId: profile.Id,
                Harness: HarnessName,
                IsDryRun: isDryRun,
                Success: false,
                Error: $"VS Code apply failed: {ex.Message}",
                Changes: new());
        }
    }

    private async Task<FileChangeResult> ProcessSettingsJsonAsync(
        string vscodeDir,
        Dictionary<string, object>? customSettings)
    {
        if (customSettings == null || customSettings.Count == 0)
        {
            return new FileChangeResult(
                FilePath: Path.Combine(vscodeDir, "settings.json"),
                Status: "unchanged");
        }

        var settingsPath = Path.Combine(vscodeDir, "settings.json");
        var existingContent = File.Exists(settingsPath) ? await File.ReadAllTextAsync(settingsPath) : "{}";

        var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(existingContent, JsonOptions)
            ?? new Dictionary<string, object>();

        // Convert and merge customSettings (handle JsonElement)
        var customSettingsDictConverted = new Dictionary<string, object>();
        foreach (var kvp in customSettings)
        {
            customSettingsDictConverted[kvp.Key] = JsonValueConverter.ConvertToObject(kvp.Value) ?? new Dictionary<string, object>();
        }

        HarnessUtilities.DeepMerge(settings, customSettingsDictConverted);

        var proposedContent = JsonSerializer.Serialize(settings, JsonOptions);
        var status = existingContent == proposedContent ? "unchanged" : (File.Exists(settingsPath) ? "modified" : "new");

        var diff = HarnessUtilities.GenerateUnifiedDiff(
            File.Exists(settingsPath) ? existingContent : null,
            proposedContent,
            settingsPath);

        return new FileChangeResult(
            FilePath: settingsPath,
            Status: status,
            ExistingContent: existingContent,
            ProposedContent: proposedContent,
            DiffOutput: diff);
    }

    private async Task<FileChangeResult> ProcessChatModelsJsonAsync(
        string vscodeDir,
        Dictionary<string, object>? chatModels)
    {
        if (chatModels == null || chatModels.Count == 0)
        {
            return new FileChangeResult(
                FilePath: Path.Combine(vscodeDir, "chatLanguageModels.json"),
                Status: "unchanged");
        }

        var modelsPath = Path.Combine(vscodeDir, "chatLanguageModels.json");
        var existingContent = File.Exists(modelsPath) ? await File.ReadAllTextAsync(modelsPath) : "{}";

        // VS Code's chatLanguageModels.json might be an array or an object
        // For dry-run, we'll just return unchanged since the format doesn't match our profile schema
        using var doc = JsonDocument.Parse(existingContent);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            // Existing file is an array, our profile uses object format, so skip merging
            return new FileChangeResult(
                FilePath: modelsPath,
                Status: "unchanged");
        }

        var models = JsonSerializer.Deserialize<Dictionary<string, object>>(existingContent, JsonOptions)
            ?? new Dictionary<string, object>();

        // Convert and merge chatModels (handle JsonElement)
        var chatModelsDictConverted = new Dictionary<string, object>();
        foreach (var kvp in chatModels)
        {
            chatModelsDictConverted[kvp.Key] = JsonValueConverter.ConvertToObject(kvp.Value) ?? new Dictionary<string, object>();
        }

        HarnessUtilities.DeepMerge(models, chatModelsDictConverted);

        var proposedContent = JsonSerializer.Serialize(models, JsonOptions);
        var status = existingContent == proposedContent ? "unchanged" : (File.Exists(modelsPath) ? "modified" : "new");

        var diff = HarnessUtilities.GenerateUnifiedDiff(
            File.Exists(modelsPath) ? existingContent : null,
            proposedContent,
            modelsPath);
        return new FileChangeResult(
            FilePath: modelsPath,
            Status: status,
            ExistingContent: existingContent,
            ProposedContent: proposedContent,
            DiffOutput: diff);
    }

    private static string? GetVscodeUserDir()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var path = HarnessUtilities.ExpandPath("~/Library/Application Support/Code/User");
            return Directory.Exists(path) ? path : null;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var path = HarnessUtilities.ExpandPath("~/.config/Code/User");
            return Directory.Exists(path) ? path : null;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Code", "User");
            return Directory.Exists(path) ? path : null;
        }

        return null;
    }

    private static string GetHypotheticalVscodeDir(bool insiders)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return insiders
                ? HarnessUtilities.ExpandPath("~/Library/Application Support/Code - Insiders/User")
                : HarnessUtilities.ExpandPath("~/Library/Application Support/Code/User");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return insiders
                ? HarnessUtilities.ExpandPath("~/.config/Code - Insiders/User")
                : HarnessUtilities.ExpandPath("~/.config/Code/User");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return insiders
                ? Path.Combine(appData, "Code - Insiders", "User")
                : Path.Combine(appData, "Code", "User");
        }

        return "";
    }

    private static string? GetVscodeInsidersUserDir()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var path = HarnessUtilities.ExpandPath("~/Library/Application Support/Code - Insiders/User");
            return Directory.Exists(path) ? path : null;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var path = HarnessUtilities.ExpandPath("~/.config/Code - Insiders/User");
            return Directory.Exists(path) ? path : null;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Code - Insiders", "User");
            return Directory.Exists(path) ? path : null;
        }

        return null;
    }

    private static Dictionary<string, object>? ConvertToDict(object? obj)
    {
        if (obj == null)
            return null;

        if (obj is Dictionary<string, object> dict)
            return dict;

        if (obj is JsonElement elem)
        {
            try
            {
                // Use GetRawText() and deserialize
                var rawText = elem.GetRawText();
                var result = JsonSerializer.Deserialize<Dictionary<string, object>>(rawText, JsonOptions);
                return result;
            }
            catch
            {
                // If that fails, try converting element directly
                if (elem.ValueKind == JsonValueKind.Object)
                {
                    var result = new Dictionary<string, object>();
                    foreach (var prop in elem.EnumerateObject())
                    {
                        result[prop.Name] = prop.Value;
                    }
                    return result;
                }
                return null;
            }
        }

        return null;
    }
}
