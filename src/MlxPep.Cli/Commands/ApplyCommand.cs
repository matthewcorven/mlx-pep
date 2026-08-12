namespace MlxPep.Cli.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MlxPep.Core;

/// <summary>
/// Handler for `mlx-pep apply` command.
/// Applies a profile harness block to local system configuration.
/// Issue #16: harness apply profile to Copilot CLI + VS Code/Insiders
/// </summary>
public class ApplyCommand
{
    public async Task<CommandResult> ExecuteAsync(
        string profilePath,
        string? harness = null,
        string? output = null,
        bool dryRun = false,
        bool backup = true,
        bool noConfirm = false,
        bool insiders = false,
        CommandContext? context = null)
    {
        context ??= new CommandContext();
        
        try
        {
            // Validate harness parameter
            if (string.IsNullOrEmpty(harness) || 
                (harness != "vscode" && harness != "copilot-cli" && harness != "opencode" && harness != "claude-code"))
            {
                var err = "Error: --harness must be 'vscode', 'copilot-cli', 'opencode', or 'claude-code'";
                if (context.JsonOutput)
                {
                    return CommandResult.Failure(err, 1);
                }
                Console.Error.WriteLine(err);
                return CommandResult.Failure(err, 1);
            }

            // Load and parse profile
            var profileReader = new ProfileReader();
            var profiles = await profileReader.ReadProfileSetAsync(profilePath);
            
            if (profiles.Count == 0)
            {
                var err = $"Error: No profiles found in {profilePath}";
                if (context.JsonOutput)
                {
                    return CommandResult.Failure(err, 1);
                }
                Console.Error.WriteLine(err);
                return CommandResult.Failure(err, 1);
            }

            var profile = profiles[0]; // Use the first profile

            // Select applier based on harness type
            IHarnessApplier applier = harness switch
            {
                "vscode" => new VscodeHarnessApplier(),
                "copilot-cli" => new CopilotCliHarnessApplier(),
                "opencode" => new OpenCodeHarnessApplier(),
                "claude-code" => new ClaudeCodeHarnessApplier(),
                _ => throw new InvalidOperationException($"Unknown harness: {harness}")
            };

            // Apply the profile
            var result = await applier.ApplyAsync(profile, isDryRun: dryRun, requestedInsiders: insiders);

            // Print dry-run output
            if (dryRun || result.Changes.Any(c => c.Status != "unchanged"))
            {
                PrintDryRunOutput(result, context.JsonOutput);
            }

            // If not dry-run and apply succeeded, print success message
            if (!dryRun && result.Success)
            {
                if (!context.JsonOutput)
                {
                    Console.WriteLine($"✅ Successfully applied profile '{profile.Id}' to {harness}");
                    if (!string.IsNullOrEmpty(result.BackupLocation))
                    {
                        Console.WriteLine($"📦 Backup created at: {result.BackupLocation}");
                    }
                }
            }

            // Output JSON if requested
            if (context.JsonOutput)
            {
                var jsonResult = new
                {
                    command = "apply",
                    status = result.Success ? "ok" : "error",
                    profileId = profile.Id,
                    harness = harness,
                    isDryRun = dryRun,
                    error = result.Error,
                    backupLocation = result.BackupLocation,
                    changes = result.Changes.Select(c => new
                    {
                        path = c.FilePath,
                        status = c.Status
                    })
                };
                Console.WriteLine(JsonSerializer.Serialize(jsonResult, new JsonSerializerOptions { WriteIndented = true }));
            }

            return result.Success 
                ? CommandResult.Success()
                : CommandResult.Failure(result.Error ?? "Apply failed", 1);
        }
        catch (Exception ex)
        {
            var err = $"Apply command failed: {ex.Message}";
            if (context.JsonOutput)
            {
                var json = new { error = err, exit_code = 1 };
                Console.WriteLine(JsonSerializer.Serialize(json));
            }
            else
            {
                Console.Error.WriteLine(err);
            }
            return CommandResult.Failure(err, 1);
        }
    }

    private static void PrintDryRunOutput(HarnessApplyResult result, bool jsonOutput)
    {
        if (jsonOutput)
            return; // JSON output already handled

        Console.WriteLine();
        Console.WriteLine("=== DRY-RUN: Harness Apply ===");
        Console.WriteLine($"Profile: {result.ProfileId}");
        Console.WriteLine($"Harness: {result.Harness}");
        Console.WriteLine();

        foreach (var change in result.Changes)
        {
            if (!string.IsNullOrEmpty(change.DiffOutput))
            {
                Console.WriteLine(change.DiffOutput);
            }
        }

        if (result.IsDryRun)
        {
            Console.WriteLine("--- No actual changes written (--dry-run) ---");
        }
        else
        {
            Console.WriteLine($"--- Changes {(result.Success ? "applied" : "failed")} ---");
        }

        Console.WriteLine();
    }
}
