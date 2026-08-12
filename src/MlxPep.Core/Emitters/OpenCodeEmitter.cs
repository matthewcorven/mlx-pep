namespace MlxPep.Core.Emitters;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Emits profiles to OpenCode format (opencode.json).
/// Issue #24: harness: OpenCode + Claude Code emitters
/// </summary>
public class OpenCodeEmitter : IHarnessEmitter
{
    public Task<string> EmitAsync(Profile profile)
    {
        var json = BuildOpenCodeConfig(profile);
        var options = new JsonSerializerOptions { WriteIndented = true };
        var result = JsonSerializer.Serialize(json, options);
        return Task.FromResult(result);
    }

    public string GetTargetFileName() => "opencode.json";

    public List<string> Validate(Profile profile)
    {
        var errors = new List<string>();

        // Validate harness has vscode config
        if (profile.Harness == null || !profile.Harness.ContainsKey("vscode"))
        {
            errors.Add("Profile harness must contain 'vscode' configuration for OpenCode emitter");
        }

        // Validate vscode has maxInputTokens and maxOutputTokens
        if (profile.Harness?.TryGetValue("vscode", out var vscodeObj) == true)
        {
            if (vscodeObj is Dictionary<string, object> vscode)
            {
                if (!vscode.ContainsKey("maxInputTokens") || !vscode.ContainsKey("maxOutputTokens"))
                {
                    errors.Add("harness.vscode must include maxInputTokens and maxOutputTokens");
                }
            }
        }

        return errors;
    }

    private JsonObject BuildOpenCodeConfig(Profile profile)
    {
        var config = new JsonObject
        {
            ["$schema"] = "https://opencode.ai/config.json",
            ["model"] = GetModelForTier(profile.Tier),
            ["metadata"] = new JsonObject
            {
                ["generatedFrom"] = profile.Id,
                ["tier"] = profile.Tier,
                ["generatedAt"] = profile.Provenance?.CreatedAt ?? DateTime.UtcNow.ToString("O")
            }
        };

        // Extract vscode settings
        if (profile.Harness?.TryGetValue("vscode", out var vscodeObj) == true &&
            vscodeObj is Dictionary<string, object> vscode)
        {
            var vscodeConfig = new JsonObject();

            if (vscode.TryGetValue("maxInputTokens", out var maxInput))
                vscodeConfig["maxTokens"] = Convert.ToInt32(maxInput);

            if (vscode.TryGetValue("maxOutputTokens", out var maxOutput))
                vscodeConfig["maxOutputTokens"] = Convert.ToInt32(maxOutput);

            config["vscode"] = vscodeConfig;
        }

        // Extract sampler settings
        if (profile.Sampler != null && profile.Sampler.Parameters != null)
        {
            var samplerConfig = new JsonObject();

            if (profile.Sampler.Parameters.TryGetValue("temperature", out var temp))
                samplerConfig["temperature"] = Convert.ToDouble(temp);

            if (profile.Sampler.Parameters.TryGetValue("topP", out var p))
                samplerConfig["topP"] = Convert.ToDouble(p);

            if (samplerConfig.Count > 0)
                config["sampler"] = samplerConfig;
        }

        // Include hardware memory if available
        if (profile.Hardware?.MemoryGb > 0)
        {
            config["hardware"] = new JsonObject { ["memory"] = profile.Hardware.MemoryGb };
        }

        return config;
    }

    private string GetModelForTier(string tier)
    {
        return tier.ToLower() switch
        {
            "efficient" => "anthropic/claude-haiku-4",
            "balanced" => "anthropic/claude-sonnet-4",
            "high" => "anthropic/claude-sonnet-4",
            _ => "anthropic/claude-sonnet-4"
        };
    }
}
