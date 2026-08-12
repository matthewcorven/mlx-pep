namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

/// <summary>
/// Applies profile harness blocks to GitHub Copilot CLI configuration.
/// Issue #16: harness apply profile to Copilot CLI + VS Code/Insiders
/// </summary>
public class CopilotCliHarnessApplier : IHarnessApplier
{
    public string HarnessName => "copilot-cli";

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
            if (profile.Harness == null || !profile.Harness.ContainsKey("copilotCli"))
            {
                return new HarnessApplyResult(
                    ProfileId: profile.Id,
                    Harness: HarnessName,
                    IsDryRun: isDryRun,
                    Success: false,
                    Error: $"Profile does not contain harness.copilotCli block",
                    Changes: new());
            }

            var copilotConfigObj = profile.Harness["copilotCli"];
            var copilotConfig = ConvertToDict(copilotConfigObj);

            if (copilotConfig == null || copilotConfig.Count == 0)
            {
                return new HarnessApplyResult(
                    ProfileId: profile.Id,
                    Harness: HarnessName,
                    IsDryRun: isDryRun,
                    Success: false,
                    Error: "harness.copilotCli is not a valid object",
                    Changes: new());
            }

            var changes = new List<FileChangeResult>();
            var profilesPath = HarnessUtilities.ExpandPath("~/.copilot/profiles.json");
            var profilesDir = Path.GetDirectoryName(profilesPath);

            if (string.IsNullOrEmpty(profilesDir))
            {
                return new HarnessApplyResult(
                    ProfileId: profile.Id,
                    Harness: HarnessName,
                    IsDryRun: isDryRun,
                    Success: false,
                    Error: "Could not determine Copilot CLI config directory",
                    Changes: new());
            }

            var change = await ProcessCopilotProfilesJsonAsync(profilesPath, profile.Id, copilotConfig);
            changes.Add(change);

            if (!isDryRun && change.Status != "unchanged")
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
                try
                {
                    if (!Directory.Exists(profilesDir))
                    {
                        Directory.CreateDirectory(profilesDir);
                    }

                    await File.WriteAllTextAsync(profilesPath, change.ProposedContent!);
                }
                catch (Exception ex)
                {
                    return new HarnessApplyResult(
                        ProfileId: profile.Id,
                        Harness: HarnessName,
                        IsDryRun: isDryRun,
                        Success: false,
                        Error: $"Failed to write {profilesPath}: {ex.Message}",
                        Changes: changes,
                        BackupLocation: backupLocation);
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
                Error: $"Copilot CLI apply failed: {ex.Message}",
                Changes: new());
        }
    }

    private async Task<FileChangeResult> ProcessCopilotProfilesJsonAsync(
        string profilesPath,
        string profileId,
        Dictionary<string, object> copilotConfig)
    {
        var existingContent = File.Exists(profilesPath)
            ? await File.ReadAllTextAsync(profilesPath)
            : "{ \"profiles\": {} }";

        var profilesData = JsonSerializer.Deserialize<Dictionary<string, object>>(existingContent, JsonOptions)
            ?? new Dictionary<string, object> { { "profiles", new Dictionary<string, object>() } };

        if (!profilesData.ContainsKey("profiles") || profilesData["profiles"] is not Dictionary<string, object> profilesDict)
        {
            profilesDict = new Dictionary<string, object>();
            profilesData["profiles"] = profilesDict;
        }

        // Create or update the profile entry with the given ID
        var profileEntry = new Dictionary<string, object>(copilotConfig);
        profileEntry["appliedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        profilesDict[profileId] = profileEntry;

        var proposedContent = JsonSerializer.Serialize(profilesData, JsonOptions);
        var status = existingContent == proposedContent ? "unchanged" : (File.Exists(profilesPath) ? "modified" : "new");

        var diff = HarnessUtilities.GenerateUnifiedDiff(
            File.Exists(profilesPath) ? existingContent : null,
            proposedContent,
            profilesPath);

        return new FileChangeResult(
            FilePath: profilesPath,
            Status: status,
            ExistingContent: existingContent,
            ProposedContent: proposedContent,
            DiffOutput: diff);
    }

    private static Dictionary<string, object>? ConvertToDict(object? obj)
    {
        if (obj == null)
            return null;

        if (obj is Dictionary<string, object> dict)
            return dict;

        if (obj is JsonElement elem)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(elem.GetRawText(), JsonOptions);
        }

        return null;
    }
}
