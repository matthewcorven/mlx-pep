using MlxPep.Core;

if (args.Length == 0)
{
    PrintHelp();
    return 0;
}

var command = args[0];

if (command == "models")
{
    if (args.Length < 2)
    {
        PrintModelsHelp();
        return 0;
    }
    
    var subcommand = args[1];
    
    if (subcommand == "list")
    {
        await HandleModelsList(args);
    }
    else if (subcommand == "get")
    {
        await HandleModelsGet(args);
    }
    else
    {
        Console.Error.WriteLine($"Unknown subcommand: {subcommand}");
        PrintModelsHelp();
        return 1;
    }
}
else if (command == "--help" || command == "-h" || command == "help")
{
    PrintHelp();
}
else
{
    Console.Error.WriteLine($"Unknown command: {command}");
    PrintHelp();
    return 1;
}

return 0;

void PrintHelp()
{
    Console.WriteLine("MLX-PEP: Apple Silicon model and profile orchestration");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  mlx-pep <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  models list       List available models in the Hugging Face cache");
    Console.WriteLine("  models get        Download a model from Hugging Face");
    Console.WriteLine("  help, --help, -h  Show this help message");
}

void PrintModelsHelp()
{
    Console.WriteLine("Models command");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  mlx-pep models list [--json]");
    Console.WriteLine("  mlx-pep models get <repo-id> [--revision <rev>] [--json]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --json, -j              Output as JSON");
    Console.WriteLine("  --revision, -r <rev>    Model revision (default: main)");
}

async Task HandleModelsList(string[] cmdArgs)
{
    var json = cmdArgs.Contains("--json") || cmdArgs.Contains("-j");
    
    var reader = new HFCacheReader();
    var models = await reader.ListModelsAsync();
    
    if (json)
    {
        var jsonModels = models.Select(m => new
        {
            m.RepoId,
            m.Revision,
            SizeBytes = m.SizeBytes,
            Size = m.GetSize(),
            LastModified = m.LastModified.ToString("O")
        });
        
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(jsonModels, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
    else
    {
        if (!models.Any())
        {
            Console.WriteLine("No models found in cache.");
            return;
        }
        
        Console.WriteLine($"{"Repo ID",-40} {"Revision",-20} {"Size",-15} {"Last Modified"}");
        Console.WriteLine(new string('-', 100));
        
        foreach (var model in models.OrderBy(m => m.RepoId))
        {
            var lastMod = model.LastModified.ToString("yyyy-MM-dd HH:mm");
            Console.WriteLine($"{model.RepoId,-40} {model.Revision,-20} {model.GetSize(),-15} {lastMod}");
        }
    }
}

async Task HandleModelsGet(string[] cmdArgs)
{
    if (cmdArgs.Length < 3)
    {
        Console.Error.WriteLine("Error: repo-id is required");
        PrintModelsHelp();
        Environment.Exit(1);
    }
    
    var repoId = cmdArgs[2];
    var json = cmdArgs.Contains("--json") || cmdArgs.Contains("-j");
    
    var revisionIdx = Array.IndexOf(cmdArgs, "--revision");
    if (revisionIdx < 0)
        revisionIdx = Array.IndexOf(cmdArgs, "-r");
    
    var revision = "main";
    if (revisionIdx >= 0 && revisionIdx + 1 < cmdArgs.Length)
        revision = cmdArgs[revisionIdx + 1];
    
    // Check if hf command is available
    if (!await IsHuggingFaceCliAvailable())
    {
        var message = "Hugging Face CLI (hf) not found. Install it with: pip install huggingface-hub";
        if (json)
        {
            var errorJson = new { error = message };
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(errorJson));
        }
        else
        {
            Console.Error.WriteLine(message);
        }
        Environment.Exit(1);
    }
    
    // Run hf download
    var result = await RunHuggingFaceDownload(repoId, revision);
    
    if (result.Success)
    {
        // Verify the model was downloaded
        var reader = new HFCacheReader();
        var model = await reader.GetModelAsync(repoId);
        
        if (model != null)
        {
            if (json)
            {
                var jsonModel = new
                {
                    success = true,
                    message = $"Successfully downloaded {repoId}@{revision}",
                    model = new
                    {
                        model.RepoId,
                        model.Revision,
                        SizeBytes = model.SizeBytes,
                        Size = model.GetSize(),
                        LastModified = model.LastModified.ToString("O")
                    }
                };
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(jsonModel, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine($"✓ Successfully downloaded {repoId}@{revision}");
                Console.WriteLine($"  Size: {model.GetSize()}");
                Console.WriteLine($"  Location: ~/.cache/huggingface/hub");
            }
        }
    }
    else
    {
        if (json)
        {
            var errorJson = new { error = result.Error };
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(errorJson));
        }
        else
        {
            Console.Error.WriteLine($"✗ Failed to download model: {result.Error}");
        }
        Environment.Exit(1);
    }
}

async Task<bool> IsHuggingFaceCliAvailable()
{
    var tcs = new TaskCompletionSource<bool>();
    
    var process = new System.Diagnostics.Process
    {
        StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "hf",
            Arguments = "--version",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }
    };
    
    process.EnableRaisingEvents = true;
    process.Exited += (sender, e) =>
    {
        tcs.TrySetResult(process.ExitCode == 0);
        process.Dispose();
    };
    
    try
    {
        process.Start();
        await Task.WhenAny(tcs.Task, Task.Delay(2000));
        return tcs.Task.IsCompleted && tcs.Task.Result;
    }
    catch
    {
        return false;
    }
}

async Task<(bool Success, string Error)> RunHuggingFaceDownload(string repoId, string revision)
{
    var tcs = new TaskCompletionSource<(bool, string)>();
    
    var process = new System.Diagnostics.Process
    {
        StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "hf",
            Arguments = $"download {repoId} --revision {revision}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }
    };
    
    var errorOutput = "";
    process.ErrorDataReceived += (sender, e) =>
    {
        if (!string.IsNullOrEmpty(e.Data))
            errorOutput += e.Data + Environment.NewLine;
    };
    
    process.EnableRaisingEvents = true;
    process.Exited += (sender, e) =>
    {
        tcs.TrySetResult((process.ExitCode == 0, errorOutput));
        process.Dispose();
    };
    
    try
    {
        process.Start();
        process.BeginErrorReadLine();
        await tcs.Task;
        return tcs.Task.Result;
    }
    catch (Exception ex)
    {
        return (false, ex.Message);
    }
}
