namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MlxPep.Core.Emitters;

/// <summary>
/// Applies a profile to OpenCode harness configuration.
/// Issue #24: OpenCode + Claude Code emitters
/// </summary>
public class OpenCodeHarnessApplier : IHarnessApplier
{
    public string HarnessName => "opencode";

    public async Task<HarnessApplyResult> ApplyAsync(
        Profile profile,
        bool isDryRun = false,
        bool requestedInsiders = false)
    {
        try
        {
            // Validate that profile has opencode harness block
            if (profile.Harness == null || !profile.Harness.ContainsKey("opencode"))
            {
                return new HarnessApplyResult(
                    ProfileId: profile.Id,
                    Harness: "opencode",
                    IsDryRun: isDryRun,
                    Success: false,
                    Error: "Profile does not contain 'opencode' harness configuration",
                    Changes: new List<FileChangeResult>());
            }

            // Determine config directory and file path
            var configDir = GetOpenCodeConfigDirectory();
            var configFile = Path.Combine(configDir, "config.json");

            // Emit the config using OpenCodeEmitter
            var emitter = new OpenCodeEmitter();
            
            // Validate profile first
            var validationErrors = emitter.Validate(profile);
            if (validationErrors.Count > 0)
            {
                return new HarnessApplyResult(
                    ProfileId: profile.Id,
                    Harness: "opencode",
                    IsDryRun: isDryRun,
                    Success: false,
                    Error: string.Join("; ", validationErrors),
                    Changes: new List<FileChangeResult>());
            }

            // Generate the new config
            var proposedContent = await emitter.EmitAsync(profile);

            // Read existing content if file exists
            string? existingContent = null;
            if (File.Exists(configFile))
            {
                existingContent = await File.ReadAllTextAsync(configFile);
            }

            // Compute diff
            var diffOutput = ComputeDiff(existingContent ?? "", proposedContent, configFile);
            
            var changes = new List<FileChangeResult>
            {
                new FileChangeResult(
                    FilePath: configFile,
                    Status: existingContent == null ? "new" : "modified",
                    ExistingContent: existingContent,
                    ProposedContent: proposedContent,
                    DiffOutput: diffOutput)
            };

            // If dry-run, return here without writing
            if (isDryRun)
            {
                return new HarnessApplyResult(
                    ProfileId: profile.Id,
                    Harness: "opencode",
                    IsDryRun: true,
                    Success: true,
                    Changes: changes);
            }

            // Create backup if file exists
            string? backupLocation = null;
            if (File.Exists(configFile))
            {
                var backupManager = new BackupManager();
                var (success, location, _) = await backupManager.CreateBackupAsync("opencode", profile.Id, changes);
                if (success)
                    backupLocation = location;
            }

            // Write the config
            Directory.CreateDirectory(configDir);
            await File.WriteAllTextAsync(configFile, proposedContent);

            return new HarnessApplyResult(
                ProfileId: profile.Id,
                Harness: "opencode",
                IsDryRun: false,
                Success: true,
                Changes: changes,
                BackupLocation: backupLocation);
        }
        catch (Exception ex)
        {
            return new HarnessApplyResult(
                ProfileId: profile.Id,
                Harness: "opencode",
                IsDryRun: isDryRun,
                Success: false,
                Error: ex.Message,
                Changes: new List<FileChangeResult>());
        }
    }

    private string GetOpenCodeConfigDirectory()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDir, ".opencode");
    }

    private string ComputeDiff(string existingContent, string proposedContent, string filePath)
    {
        if (string.IsNullOrEmpty(existingContent))
        {
            return $"New file: {filePath}\n+++ {filePath}\n@@ -0,0 +1,{proposedContent.Count(c => c == '\n') + 1} @@\n" +
                   string.Join("\n", proposedContent.Split('\n').Select(l => "+ " + l));
        }

        // Simple diff: show lines that differ
        var existingLines = existingContent.Split('\n');
        var proposedLines = proposedContent.Split('\n');

        var diff = new List<string> { $"--- {filePath}", $"+++ {filePath}" };
        
        var maxLines = Math.Max(existingLines.Length, proposedLines.Length);
        for (int i = 0; i < maxLines; i++)
        {
            var existing = i < existingLines.Length ? existingLines[i] : "";
            var proposed = i < proposedLines.Length ? proposedLines[i] : "";

            if (existing != proposed)
            {
                if (!string.IsNullOrEmpty(existing))
                    diff.Add("- " + existing);
                if (!string.IsNullOrEmpty(proposed))
                    diff.Add("+ " + proposed);
            }
        }

        return string.Join("\n", diff);
    }
}
