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

        // Validate harness has opencode config
        if (profile.Harness == null || !profile.Harness.ContainsKey("opencode"))
        {
            errors.Add("Profile harness must contain 'opencode' configuration for OpenCode emitter");
        }

        // Validate opencode has maxInputTokens and maxOutputTokens
        if (profile.Harness?.TryGetValue("opencode", out var opencodeObj) == true)
        {
            if (opencodeObj is Dictionary<string, object> opencode)
            {
                if (!opencode.ContainsKey("maxInputTokens") || !opencode.ContainsKey("maxOutputTokens"))
                {
                    errors.Add("harness.opencode must include maxInputTokens and maxOutputTokens");
                }
            }
        }

        return errors;
    }

    private JsonObject BuildOpenCodeConfig(Profile profile)
    {
        var metadata = new JsonObject
        {
            ["generatedFrom"] = profile.Id,
            ["tier"] = profile.Tier,
            ["generatedAt"] = DateTime.UtcNow.ToString("O")
        };

        if (profile.Hardware != null)
        {
            metadata["memoryGb"] = profile.Hardware.MemoryGb;
        }

        var config = new JsonObject
        {
            ["$schema"] = "https://opencode.ai/config.json",
            ["model"] = GetModelForTier(profile.Tier),
            ["metadata"] = metadata
        };

        // Extract opencode settings
        if (profile.Harness?.TryGetValue("opencode", out var opencodeObj) == true &&
            opencodeObj is Dictionary<string, object> opencode)
        {
            var opencodeConfig = new JsonObject();

            if (opencode.TryGetValue("maxInputTokens", out var maxInput))
                opencodeConfig["maxTokens"] = Convert.ToInt32(maxInput);

            if (opencode.TryGetValue("maxOutputTokens", out var maxOutput))
                opencodeConfig["maxOutputTokens"] = Convert.ToInt32(maxOutput);

            config["vscode"] = opencodeConfig;
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
