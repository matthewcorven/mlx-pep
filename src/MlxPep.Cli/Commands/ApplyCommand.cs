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
        using var progress = context.CreateProgressScope("apply", 5);

        try
        {
            context.Verbose("ApplyCommand", $"Starting apply command for profilePath='{profilePath}', harness='{harness}', dryRun={dryRun}, insiders={insiders}.");
            progress.StartStep("validate apply arguments");
            // Validate harness parameter
            if (string.IsNullOrEmpty(harness) ||
                (harness != "vscode" && harness != "copilot-cli" && harness != "opencode" && harness != "claude-code"))
            {
                context.Verbose("ApplyCommand", "Harness validation failed; returning argument error.");
                progress.CompleteStep("apply argument validation failed");
                var err = "Error: --harness must be 'vscode', 'copilot-cli', 'opencode', or 'claude-code'";
                if (context.JsonOutput)
                {
                    return CommandResult.Failure(err, 1);
                }
                Console.Error.WriteLine(err);
                return CommandResult.Failure(err, 1);
            }
            progress.CompleteStep("apply arguments validated");

            // Load and parse profile
            progress.StartStep("load profile set");
            var profileReader = new ProfileReader();
            var profiles = await profileReader.ReadProfileSetAsync(profilePath);

            if (profiles.Count == 0)
            {
                context.Verbose("ApplyCommand", "Profile set was empty after parsing.");
                progress.CompleteStep("profile set load failed");
                var err = $"Error: No profiles found in {profilePath}";
                if (context.JsonOutput)
                {
                    return CommandResult.Failure(err, 1);
                }
                Console.Error.WriteLine(err);
                return CommandResult.Failure(err, 1);
            }
            context.Verbose("ApplyCommand", $"Loaded {profiles.Count} profile records from '{profilePath}'.");
            progress.CompleteStep($"loaded {profiles.Count} profile records");

            progress.StartStep("build apply profile");
            var profile = HarnessProfileSetBuilder.BuildApplyProfile(profiles, harness);
            progress.CompleteStep($"built apply profile '{profile.Id}'");

            // Select applier based on harness type
            progress.StartStep("apply harness changes");
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
            progress.CompleteStep($"apply operation returned success={result.Success}");

            // Print dry-run output
            if (dryRun || result.Changes.Any(c => c.Status != "unchanged"))
            {
                context.Verbose("ApplyCommand", "Rendering dry-run or changed-file output.");
                PrintDryRunOutput(result, context.JsonOutput);
            }
            else
            {
                context.Verbose("ApplyCommand", "No changed files required dry-run rendering.");
            }

            // If not dry-run and apply succeeded, print success message
            if (!dryRun && result.Success)
            {
                if (!context.JsonOutput)
                {
                    context.Verbose("ApplyCommand", "Apply succeeded and text output branch is active.");
                    Console.WriteLine($"✅ Successfully applied profile '{profile.Id}' to {harness}");
                    if (!string.IsNullOrEmpty(result.BackupLocation))
                    {
                        context.Verbose("ApplyCommand", $"Backup emitted at '{result.BackupLocation}'.");
                        Console.WriteLine($"📦 Backup created at: {result.BackupLocation}");
                    }
                    else
                    {
                        context.Verbose("ApplyCommand", "No backup path was returned from the harness applier.");
                    }
                }
            }
            else
            {
                context.Verbose("ApplyCommand", "Apply command skipped text success output because it was a dry run or the apply failed.");
            }

            // Output JSON if requested
            if (context.JsonOutput)
            {
                context.Verbose("ApplyCommand", "JSON output branch selected for apply command.");
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
            context.Verbose("ApplyCommand", $"Apply command failed with {ex.GetType().Name}: {ex.Message}");
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
        finally
        {
            context.Verbose("ApplyCommand", "Apply command finished execution path.");
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
