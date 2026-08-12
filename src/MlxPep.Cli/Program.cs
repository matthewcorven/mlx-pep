using System.Text.Json;
using MlxPep.Core;
using MlxPep.Core.Emitters;

var cmdArgs = args.ToList();

if (cmdArgs.Count == 0 || cmdArgs[0] != "apply")
{
    Console.WriteLine("mlx-pep - ML Model Export Platform");
    Console.WriteLine("\nUsage: mlx-pep apply --profile <path> --tier <tier> --harness <format> [--output <path>] [--dry-run] [--backup]");
    Console.WriteLine("\nOptions:");
    Console.WriteLine("  --profile, -p   Path to profile JSONL file");
    Console.WriteLine("  --tier, -t      Profile tier (high, balanced, efficient) [default: balanced]");
    Console.WriteLine("  --harness, -h   Target format: opencode, claude-code [default: opencode]");
    Console.WriteLine("  --output, -o    Output file path");
    Console.WriteLine("  --dry-run       Preview output without writing");
    Console.WriteLine("  --backup        Create backup before overwriting [default: true]");
    return;
}

// Parse command line arguments
string? profilePath = null;
string tier = "balanced";
string harness = "opencode";
string? outputPath = null;
bool dryRun = false;
bool backup = true;

for (int i = 1; i < cmdArgs.Count; i++)
{
    switch (cmdArgs[i])
    {
        case "--profile" or "-p":
            if (i + 1 < cmdArgs.Count) profilePath = cmdArgs[++i];
            break;
        case "--tier" or "-t":
            if (i + 1 < cmdArgs.Count) tier = cmdArgs[++i];
            break;
        case "--harness" or "-h":
            if (i + 1 < cmdArgs.Count) harness = cmdArgs[++i];
            break;
        case "--output" or "-o":
            if (i + 1 < cmdArgs.Count) outputPath = cmdArgs[++i];
            break;
        case "--dry-run":
            dryRun = true;
            break;
        case "--backup":
            backup = true;
            break;
    }
}

try
{
    // Validate inputs
    if (string.IsNullOrEmpty(profilePath))
    {
        Console.Error.WriteLine("Error: --profile is required");
        Environment.Exit(1);
    }

    var profileFile = new FileInfo(profilePath);
    if (!profileFile.Exists)
    {
        Console.Error.WriteLine($"Error: Profile file not found: {profileFile.FullName}");
        Environment.Exit(1);
    }

    if (!IsValidTier(tier))
    {
        Console.Error.WriteLine($"Error: Invalid tier '{tier}'. Must be high, balanced, or efficient.");
        Environment.Exit(1);
    }

    if (!IsValidHarness(harness))
    {
        Console.Error.WriteLine($"Error: Invalid harness format '{harness}'. Must be opencode or claude-code.");
        Environment.Exit(1);
    }

    // Parse profile from JSONL
    var profile = FindAndParseProfile(profileFile, tier);
    if (profile == null)
    {
        Console.Error.WriteLine($"Error: Could not parse profile with tier '{tier}' from {profileFile.FullName}");
        Environment.Exit(1);
    }

    // Select emitter
    IHarnessEmitter emitter = harness.Equals("claude-code", StringComparison.OrdinalIgnoreCase)
        ? new ClaudeCodeEmitter()
        : new OpenCodeEmitter();

    // Validate profile
    var errors = emitter.Validate(profile);
    if (errors.Count > 0)
    {
        Console.Error.WriteLine("Error: Profile validation failed:");
        foreach (var error in errors)
        {
            Console.Error.WriteLine($"  - {error}");
        }
        Environment.Exit(1);
    }

    // Emit to format
    var emittedJson = await emitter.EmitAsync(profile);

    // Determine output path
    FileInfo finalOutputPath;
    if (!string.IsNullOrEmpty(outputPath))
    {
        finalOutputPath = new FileInfo(outputPath);
    }
    else
    {
        var fileName = emitter.GetTargetFileName();
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        finalOutputPath = harness.Equals("claude-code", StringComparison.OrdinalIgnoreCase)
            ? new FileInfo(Path.Combine(homeDir, ".claude", fileName))
            : new FileInfo(Path.Combine(homeDir, ".config", "opencode", fileName));
    }

    // Handle dry-run
    if (dryRun)
    {
        Console.WriteLine("=== DRY RUN: Preview output ===\n");
        Console.WriteLine(emittedJson);
        Console.WriteLine("\n=== Would be written to: ===");
        Console.WriteLine(finalOutputPath.FullName);
        return;
    }

    // Create backup if file exists
    if (finalOutputPath.Exists && backup)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var backupPath = $"{finalOutputPath.FullName}.backup.{timestamp}";
        File.Copy(finalOutputPath.FullName, backupPath, overwrite: true);
        Console.WriteLine($"✓ Created backup: {backupPath}");
    }

    // Write output
    finalOutputPath.Directory?.Create();
    File.WriteAllText(finalOutputPath.FullName, emittedJson);
    Console.WriteLine($"✓ Successfully wrote {harness} config to: {finalOutputPath.FullName}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
}

// ===== Helper functions =====

bool IsValidTier(string t) =>
    t.Equals("high", StringComparison.OrdinalIgnoreCase) ||
    t.Equals("balanced", StringComparison.OrdinalIgnoreCase) ||
    t.Equals("efficient", StringComparison.OrdinalIgnoreCase);

bool IsValidHarness(string h) =>
    h.Equals("opencode", StringComparison.OrdinalIgnoreCase) ||
    h.Equals("claude-code", StringComparison.OrdinalIgnoreCase);

Profile? FindAndParseProfile(FileInfo file, string targetTier)
{
    try
    {
        var lines = File.ReadAllLines(file.FullName);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var json = JsonDocument.Parse(line);
            var root = json.RootElement;

            if (root.TryGetProperty("tier", out var tierElement))
            {
                var tierValue = tierElement.GetString() ?? "";
                if (tierValue.Equals(targetTier, StringComparison.OrdinalIgnoreCase))
                {
                    return ParseProfileFromJson(root);
                }
            }
        }

        return null;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Debug: Profile parsing error: {ex.Message}");
        return null;
    }
}

Profile ParseProfileFromJson(JsonElement root)
{
    var schemaVersion = root.TryGetProperty("schemaVersion", out var sv) ? sv.GetInt32() : 1;
    var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
    var modelHfId = root.TryGetProperty("modelHfId", out var mhf) ? mhf.GetString() ?? "" : "";
    var tier = root.TryGetProperty("tier", out var t) ? t.GetString() ?? "balanced" : "balanced";
    var engine = root.TryGetProperty("engine", out var e) ? e.GetString() ?? "" : "";
    
    var system = JsonElementToDictionary(root, "system");
    var omlx = JsonElementToDictionary(root, "omlx");
    var harness = JsonElementToDictionary(root, "harness");
    
    var provenance = ParseProvenance(root);
    var hardware = ParseHardware(root);
    var sampler = ParseSampler(root);
    
    return new Profile(
        SchemaVersion: schemaVersion,
        Id: id,
        ModelHfId: modelHfId,
        Tier: tier,
        Engine: engine,
        System: system,
        OMLXSettings: omlx,
        Harness: harness,
        Provenance: provenance,
        Hardware: hardware,
        Sampler: sampler
    );
}

Dictionary<string, object> JsonElementToDictionary(JsonElement root, string property)
{
    var dict = new Dictionary<string, object>();
    
    if (!root.TryGetProperty(property, out var element))
        return dict;
        
    if (element.ValueKind != JsonValueKind.Object)
        return dict;

    foreach (var prop in element.EnumerateObject())
    {
        dict[prop.Name] = JsonElementToObject(prop.Value);
    }
    
    return dict;
}

object JsonElementToObject(JsonElement element)
{
    return element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? "",
        JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
        JsonValueKind.Object => JsonElementToDictNested(element),
        _ => ""
    };
}

Dictionary<string, object> JsonElementToDictNested(JsonElement element)
{
    var dict = new Dictionary<string, object>();
    foreach (var prop in element.EnumerateObject())
    {
        dict[prop.Name] = JsonElementToObject(prop.Value);
    }
    return dict;
}

ProfileProvenance ParseProvenance(JsonElement root)
{
    var author = "";
    var createdAt = "";
    var source = "";
    
    if (root.TryGetProperty("provenance", out var prov))
    {
        if (prov.TryGetProperty("author", out var a))
            author = a.GetString() ?? "";
        if (prov.TryGetProperty("createdAt", out var c))
            createdAt = c.GetString() ?? "";
        if (prov.TryGetProperty("source", out var s))
            source = s.GetString() ?? "";
    }
    
    return new ProfileProvenance(author, createdAt, source);
}

HardwareFingerprint ParseHardware(JsonElement root)
{
    var chip = "Unknown";
    var memoryGb = 0;
    var modelIdentifier = "Unknown";
    
    if (root.TryGetProperty("hardware", out var hw))
    {
        if (hw.TryGetProperty("chip", out var c))
            chip = c.GetString() ?? "Unknown";
        if (hw.TryGetProperty("memoryGb", out var m))
            memoryGb = m.GetInt32();
        if (hw.TryGetProperty("modelIdentifier", out var mi))
            modelIdentifier = mi.GetString() ?? "Unknown";
    }
    
    return new HardwareFingerprint(chip, memoryGb, modelIdentifier);
}

SamplerSettings? ParseSampler(JsonElement root)
{
    if (!root.TryGetProperty("sampler", out var samplerEl))
        return null;

    var type = "default";
    Dictionary<string, object>? parameters = null;
    
    if (samplerEl.TryGetProperty("type", out var t))
        type = t.GetString() ?? "default";
    
    if (samplerEl.TryGetProperty("parameters", out var p) && p.ValueKind == JsonValueKind.Object)
    {
        parameters = new Dictionary<string, object>();
        foreach (var prop in p.EnumerateObject())
        {
            parameters[prop.Name] = JsonElementToObject(prop.Value);
        }
        if (parameters.Count == 0)
            parameters = null;
    }
    
    return new SamplerSettings(type, parameters);
}
