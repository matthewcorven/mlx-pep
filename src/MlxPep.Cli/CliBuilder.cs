using System.Text.Json;
using MlxPep.Cli.Commands;

namespace MlxPep.Cli;

/// <summary>
/// Command-line interface router for mlx-pep.
/// Parses arguments and dispatches to handler classes.
/// </summary>
public static class CliBuilder
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        bool isJson = args.Contains("--json");
        string command = args[0].ToLowerInvariant();

        try
        {
            return await (command switch
            {
                "doctor" => HandleDoctor(isJson),
                "models" => HandleModels(args.Skip(1).ToArray(), isJson),
                "profiles" => HandleProfiles(args.Skip(1).ToArray(), isJson),
                "apply" => HandleApply(args.Skip(1).ToArray(), isJson),
                "assess" => HandleAssess(args.Skip(1).ToArray(), isJson),
                "tui" => HandleTui(isJson),
                "--help" or "-h" or "help" => Task.FromResult(PrintHelpAndReturn0()),
                "--version" or "-v" or "version" => Task.FromResult(PrintVersionAndReturn0()),
                _ => Task.FromResult(PrintErrorAndReturn1($"Unknown command: {command}"))
            });
        }
        catch (Exception ex)
        {
            if (isJson)
            {
                var errorJson = new { error = ex.Message, exit_code = 1 };
                Console.WriteLine(JsonSerializer.Serialize(errorJson));
            }
            else
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }
            return 1;
        }
    }

    private static int PrintHelpAndReturn0()
    {
        PrintHelp();
        return 0;
    }

    private static int PrintVersionAndReturn0()
    {
        Console.WriteLine("mlx-pep 0.1.0-alpha");
        return 0;
    }

    private static int PrintErrorAndReturn1(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static async Task<int> HandleDoctor(bool isJson)
    {
        var handler = new DoctorCommand();
        var context = new CommandContext(isJson);
        var result = await handler.ExecuteAsync(context);

        return result.ExitCode;
    }

    private static async Task<int> HandleModels(string[] args, bool isJson)
    {
        if (args.Length == 0)
        {
            return PrintErrorAndReturn1("Usage: mlx-pep models [list|get]");
        }

        string subcommand = args[0].ToLowerInvariant();
        CommandResult result = subcommand switch
        {
            "list" => await new ModelsListCommand().ExecuteAsync(new CommandContext(isJson)),
            "get" => args.Length > 1
                ? await new ModelsGetCommand().ExecuteAsync(args[1], new CommandContext(isJson))
                : new CommandResult(1, "Usage: mlx-pep models get <hf_id>"),
            _ => new CommandResult(1, $"Unknown models subcommand: {subcommand}")
        };

        if (isJson)
        {
            var json = new { message = result.Message, exit_code = result.ExitCode };
            Console.WriteLine(JsonSerializer.Serialize(json));
        }
        else
        {
            Console.WriteLine(result.Message);
        }
        return result.ExitCode;
    }

    private static async Task<int> HandleProfiles(string[] args, bool isJson)
    {
        if (args.Length == 0)
        {
            return PrintErrorAndReturn1("Usage: mlx-pep profiles [list|search|pull] [--local]");
        }

        string subcommand = args[0].ToLowerInvariant();
        CommandResult result = subcommand switch
        {
            "list" => await new ProfilesListCommand().ExecuteAsync(new CommandContext(isJson), args.Contains("--local")),
            "search" => args.Length > 1
                ? await new ProfilesSearchCommand().ExecuteAsync(args[1], new CommandContext(isJson))
                : new CommandResult(1, "Usage: mlx-pep profiles search <query>"),
            "pull" => args.Length > 1
                ? await new ProfilesPullCommand().ExecuteAsync(args[1], new CommandContext(isJson))
                : new CommandResult(1, "Usage: mlx-pep profiles pull <profile_id>"),
            _ => new CommandResult(1, $"Unknown profiles subcommand: {subcommand}")
        };

        if (isJson)
        {
            var json = new { message = result.Message, exit_code = result.ExitCode };
            Console.WriteLine(JsonSerializer.Serialize(json));
        }
        else if (result.ExitCode != 0)
        {
            Console.WriteLine(result.Message);
        }
        return result.ExitCode;
    }

    private static async Task<int> HandleApply(string[] args, bool isJson)
    {
        if (args.Length == 0)
        {
            return PrintErrorAndReturn1("Usage: mlx-pep apply <profile_file> --harness vscode|copilot-cli [--insiders] [--dry-run] [--no-confirm]");
        }

        string profile = args[0];
        string? harness = GetOptionValue(args, "--harness");
        string? output = GetOptionValue(args, "--output");
        bool dryRun = args.Contains("--dry-run");
        bool backup = true;
        bool noConfirm = args.Contains("--no-confirm");
        bool insiders = args.Contains("--insiders");

        var handler = new ApplyCommand();
        var context = new CommandContext(isJson);
        var result = await handler.ExecuteAsync(profile, harness, output, dryRun, backup, noConfirm, insiders, context);

        return result.ExitCode;
    }

    private static async Task<int> HandleAssess(string[] args, bool isJson)
    {
        if (args.Length == 0)
        {
            return PrintErrorAndReturn1("Usage: mlx-pep assess <hf_id> [--assistant-model-id X] [--suite smoke|full] [--publish]");
        }

        string hfId = args[0];
        string? assistantModelId = GetOptionValue(args, "--assistant-model-id");
        string suite = GetOptionValue(args, "--suite") ?? "full";
        bool publish = args.Contains("--publish");

        // Validate suite argument
        if (suite != "smoke" && suite != "full")
        {
            return PrintErrorAndReturn1("Error: --suite must be 'smoke' or 'full'");
        }

        var handler = new AssessCommand();
        var context = new CommandContext(isJson);
        var result = await handler.ExecuteAsync(hfId, assistantModelId, suite, publish, context);

        // AssessCommand handles its own JSON output; don't double-output
        if (!isJson && result.ExitCode != 0)
        {
            Console.WriteLine(result.Message);
        }
         
        return result.ExitCode;
    }

    private static async Task<int> HandleTui(bool isJson)
    {
        var handler = new TuiCommand();
        var context = new CommandContext(isJson);
        var result = await handler.ExecuteAsync(context);

        if (isJson)
        {
            var json = new { message = result.Message, exit_code = result.ExitCode };
            Console.WriteLine(JsonSerializer.Serialize(json));
        }
        else
        {
            Console.WriteLine(result.Message);
        }
        return result.ExitCode;
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"
mlx-pep — MLX Performance Evaluation Platform
 
USAGE:
    mlx-pep <COMMAND> [OPTIONS] [--json]
 
COMMANDS:
    doctor              Diagnose system and environment
    models list         List available HF models
    models get <id>     Get model info by HF ID
    profiles list       List all profiles
    profiles search <q> Search profiles by query
    profiles pull <id>  Pull profile from registry
    apply <file>        Apply profile to harness (--dry-run, --harness)
    assess <hf_id>      Assess model performance (--assistant-model-id, --suite, --publish)
    tui                 Start terminal UI
    help                Show this help
    --version           Show version
 
OPTIONS:
    --json                    Output JSON (available on all commands)
    --dry-run                 (apply) Show changes without applying
    --harness <name>          (apply) Target harness (vscode, copilot-cli)
    --assistant-model-id <id> (assess) Optional assistant model HF ID
    --suite <suite>           (assess) Assessment suite (smoke or full, default: full)
    --publish                 (assess) Publish results to service
    --help, -h                Show this help
 
EXAMPLES:
    mlx-pep doctor
    mlx-pep models list --json
    mlx-pep apply my-profile.jsonl --harness copilot-cli
    mlx-pep assess meta-llama/Llama-2-7b
    mlx-pep assess meta-llama/Llama-2-7b --suite smoke --publish
    mlx-pep assess meta-llama/Llama-2-7b --assistant-model-id mistral/mistral-7b-v0.1
");
    }

    private static string? GetOptionValue(string[] args, string optionName)
    {
        int index = Array.IndexOf(args, optionName);
        if (index >= 0 && index < args.Length - 1)
        {
            return args[index + 1];
        }
        return null;
    }
}
