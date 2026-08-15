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
        var invocation = CliRuntime.ParseInvocation(args);
        var commandArgs = invocation.CommandArgs;
        var context = invocation.Context;

        if (args.Length == 0 || commandArgs.Length == 0)
        {
            context.Verbose("CliBuilder", "No command arguments remained after parsing global options; printing help.");
            PrintHelp();
            return 0;
        }

        string command = commandArgs[0].ToLowerInvariant();
        context.Verbose("CliBuilder", $"Parsed command '{command}' with json={context.JsonOutput}, verbose={context.VerboseOutput}, progress={context.ProgressOutput}.");

        try
        {
            return await (command switch
            {
                "doctor" => HandleDoctor(context),
                "config" => HandleConfig(context),
                "models" => HandleModels(commandArgs.Skip(1).ToArray(), context),
                "profiles" => HandleProfiles(commandArgs.Skip(1).ToArray(), context),
                "results" => HandleResults(commandArgs.Skip(1).ToArray(), context),
                "apply" => HandleApply(commandArgs.Skip(1).ToArray(), context),
                "assess" => HandleAssess(commandArgs.Skip(1).ToArray(), context),
                "tui" => HandleTui(context),
                "--help" or "-h" or "help" => Task.FromResult(PrintHelpAndReturn0()),
                "--version" or "-v" or "version" => Task.FromResult(PrintVersionAndReturn0()),
                _ => Task.FromResult(PrintErrorAndReturn1($"Unknown command: {command}"))
            });
        }
        catch (Exception ex)
        {
            context.Verbose("CliBuilder", $"Unhandled exception while routing command '{command}': {ex.GetType().Name}: {ex.Message}");
            if (context.JsonOutput)
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

    private static async Task<int> HandleDoctor(CommandContext context)
    {
        var handler = new DoctorCommand();
        var result = await handler.ExecuteAsync(context);

        return result.ExitCode;
    }

    private static async Task<int> HandleConfig(CommandContext context)
    {
        var handler = new ConfigCommand();
        var result = await handler.ExecuteAsync(context);

        if (!context.JsonOutput && !string.IsNullOrWhiteSpace(result.Message))
        {
            Console.WriteLine(result.Message);
        }

        return result.ExitCode;
    }

    private static async Task<int> HandleModels(string[] args, CommandContext context)
    {
        if (args.Length == 0)
        {
            return PrintErrorAndReturn1("Usage: mlx-pep models [list|get|status]");
        }

        string subcommand = args[0].ToLowerInvariant();
        CommandResult result = subcommand switch
        {
            "list" => await new ModelsListCommand().ExecuteAsync(context),
            "get" => args.Length > 1
                ? await new ModelsGetCommand().ExecuteAsync(args[1], context, waitForCompletion: !args.Contains("--no-wait"), loadAfterDownload: args.Contains("--load"))
                : new CommandResult(1, "Usage: mlx-pep models get <hf_id>"),
            "status" => await new ModelsStatusCommand().ExecuteAsync(context),
            _ => new CommandResult(1, $"Unknown models subcommand: {subcommand}")
        };

        if (!context.JsonOutput && !string.IsNullOrWhiteSpace(result.Message))
        {
            Console.WriteLine(result.Message);
        }

        return result.ExitCode;
    }

    private static async Task<int> HandleProfiles(string[] args, CommandContext context)
    {
        if (args.Length == 0)
        {
            return PrintErrorAndReturn1("Usage: mlx-pep profiles [list|search|pull] [--local]");
        }

        string subcommand = args[0].ToLowerInvariant();
        CommandResult result = subcommand switch
        {
            "list" => await new ProfilesListCommand().ExecuteAsync(context, args.Contains("--local")),
            "search" => args.Length > 1
                ? await new ProfilesSearchCommand().ExecuteAsync(args[1], context)
                : new CommandResult(1, "Usage: mlx-pep profiles search <query>"),
            "pull" => args.Length > 1
                ? await new ProfilesPullCommand().ExecuteAsync(args[1], context)
                : new CommandResult(1, "Usage: mlx-pep profiles pull <profile_id>"),
            _ => new CommandResult(1, $"Unknown profiles subcommand: {subcommand}")
        };

        if (context.JsonOutput)
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

    private static async Task<int> HandleApply(string[] args, CommandContext context)
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
        var result = await handler.ExecuteAsync(profile, harness, output, dryRun, backup, noConfirm, insiders, context);

        return result.ExitCode;
    }

    private static async Task<int> HandleAssess(string[] args, CommandContext context)
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
        var result = await handler.ExecuteAsync(hfId, assistantModelId, suite, publish, context);

        // AssessCommand handles its own JSON output; don't double-output
        if (!context.JsonOutput && result.ExitCode != 0)
        {
            Console.WriteLine(result.Message);
        }
         
        return result.ExitCode;
    }

    private static async Task<int> HandleResults(string[] args, CommandContext context)
    {
        var subcommand = args.Length == 0 ? "list" : args[0].ToLowerInvariant();
        var remainingArgs = args.Length == 0 ? Array.Empty<string>() : args.Skip(1).ToArray();

        var result = await (subcommand switch
        {
            "list" => new ResultsListCommand().ExecuteAsync(
                context,
                includeIncomplete: remainingArgs.Contains("--all"),
                modelId: GetOptionValue(remainingArgs, "--model")),

            "show" => new ResultsShowCommand().ExecuteAsync(
                context,
                runId: GetFirstPositionalArg(remainingArgs, "--model"),
                modelId: GetOptionValue(remainingArgs, "--model"),
                includeIncomplete: remainingArgs.Contains("--all")),

            "export" => new ResultsExportCommand().ExecuteAsync(
                context,
                outputPath: GetOptionValue(remainingArgs, "--output") ?? string.Empty,
                runId: GetFirstPositionalArg(remainingArgs, "--model", "--output", "--format"),
                modelId: GetOptionValue(remainingArgs, "--model"),
                format: GetOptionValue(remainingArgs, "--format") ?? "markdown",
                includeIncomplete: remainingArgs.Contains("--all")),

            _ => Task.FromResult(CommandResult.Failure("Usage: mlx-pep results [list|show|export] [options]"))
        });

        if (!context.JsonOutput && result.ExitCode != 0 && !string.IsNullOrWhiteSpace(result.Message))
        {
            Console.WriteLine(result.Message);
        }

        return result.ExitCode;
    }

    private static async Task<int> HandleTui(CommandContext context)
    {
        var handler = new TuiCommand();
        var result = await handler.ExecuteAsync(context);

        if (context.JsonOutput)
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
    mlx-pep [--json] [--progress] [--verbose] <COMMAND> [OPTIONS]
 
COMMANDS:
    doctor              Diagnose system and environment
    config              Show current oMLX environment configuration
    models list         List available HF cache models
    models get <id>     Download a model through the oMLX admin API
    models status       Show oMLX download tasks and model load state
    profiles list       List all profiles
    profiles search <q> Search profiles by query
    profiles pull <id>  Pull profile from registry
    results list        List local assessment runs (verified complete by default)
    results show        Show a local assessment run summary (latest or by run id)
    results export      Save a local assessment run summary as markdown or json
    apply <file>        Apply profile to harness (--dry-run, --harness)
    assess <hf_id>      Assess model performance (--assistant-model-id, --suite, --publish)
    tui                 Start terminal UI
    help                Show this help
    --version           Show version
 
OPTIONS:
    --json                    Output JSON (available on all commands)
    --progress                Write stderr progress updates with work and overall percentages
    --verbose                 Write very chatty stderr diagnostics and Debug/Trace output
    --dry-run                 (apply) Show changes without applying
    --harness <name>          (apply) Target harness (vscode, copilot-cli)
    --assistant-model-id <id> (assess) Optional assistant model HF ID
    --suite <suite>           (assess) Assessment suite (smoke or full, default: full)
    --publish                 (assess) Publish results to service
    --model <hf_id>           (results) Filter or select latest run by model id
    --all                     (results) Include incomplete local runs
    --output <path>           (results export) Output file path
    --format <fmt>            (results export) markdown or json
    --help, -h                Show this help
 
EXAMPLES:
    mlx-pep doctor
    mlx-pep config
    mlx-pep --verbose --progress models get mlx-community/Qwen3-0.6B-4bit --no-wait
    mlx-pep models list --json
    mlx-pep apply my-profile.jsonl --harness copilot-cli
    mlx-pep assess meta-llama/Llama-2-7b
    mlx-pep results list
    mlx-pep results show --model mlx-community/NVIDIA-Nemotron-3.5-Lightning-30B-A3B-4bit
    mlx-pep results export 20260814-122505-nvidia-nemotron-3-5-lightning-30b-a3b-4bit-smoke --output ./nemotron.md --format markdown
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

    private static string? GetFirstPositionalArg(string[] args, params string[] optionsWithValues)
    {
        var optionSet = new HashSet<string>(optionsWithValues, StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                if (optionSet.Contains(arg))
                {
                    index++;
                }

                continue;
            }

            return arg;
        }

        return null;
    }
}
