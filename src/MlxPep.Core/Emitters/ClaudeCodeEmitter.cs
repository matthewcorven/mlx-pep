namespace MlxPep.Core.Emitters;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Emits profiles to Claude Code format (settings.json).
/// Issue #24: harness: OpenCode + Claude Code emitters
/// </summary>
public class ClaudeCodeEmitter : IHarnessEmitter
{
    public Task<string> EmitAsync(Profile profile)
    {
        var json = BuildClaudeCodeConfig(profile);
        var options = new JsonSerializerOptions { WriteIndented = true };
        var result = JsonSerializer.Serialize(json, options);
        return Task.FromResult(result);
    }

    public string GetTargetFileName() => "settings.json";

    public List<string> Validate(Profile profile)
    {
        var errors = new List<string>();

        // Validate harness has vscode config
        if (profile.Harness == null || !profile.Harness.ContainsKey("vscode"))
        {
            errors.Add("Profile harness must contain 'vscode' configuration for Claude Code emitter");
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

    private JsonObject BuildClaudeCodeConfig(Profile profile)
    {
        var config = new JsonObject
        {
            ["model"] = GetModelForTier(profile.Tier),
            ["metadata"] = new JsonObject
            {
                ["generatedFrom"] = profile.Id,
                ["modelId"] = profile.ModelHfId,
                ["tier"] = profile.Tier,
                ["engine"] = profile.Engine,
                ["generatedAt"] = profile.Provenance?.CreatedAt ?? DateTime.UtcNow.ToString("O")
            }
        };

        // Extract vscode settings for limits
        if (profile.Harness?.TryGetValue("vscode", out var vscodeObj) == true &&
            vscodeObj is Dictionary<string, object> vscode)
        {
            var limits = new JsonObject();

            if (vscode.TryGetValue("maxInputTokens", out var maxInput))
                limits["maxInputTokens"] = Convert.ToInt32(maxInput);

            if (vscode.TryGetValue("maxOutputTokens", out var maxOutput))
                limits["maxOutputTokens"] = Convert.ToInt32(maxOutput);

            if (limits.Count > 0)
                config["limits"] = limits;
        }

        // Extract sampler settings for inference
        if (profile.Sampler != null && profile.Sampler.Parameters != null && profile.Sampler.Parameters.Count > 0)
        {
            var inference = new JsonObject();

            if (profile.Sampler.Parameters.TryGetValue("temperature", out var temp))
                inference["temperature"] = Convert.ToDouble(temp);

            if (profile.Sampler.Parameters.TryGetValue("topP", out var topP))
                inference["topP"] = Convert.ToDouble(topP);

            if (profile.Sampler.Parameters.TryGetValue("topK", out var topK))
                inference["topK"] = Convert.ToInt32(topK);

            if (inference.Count > 0)
                config["inference"] = inference;
        }

        // Build environment variables
        var env = new JsonObject
        {
            ["CLAUDE_CODE_TIER"] = profile.Tier,
            ["CLAUDE_CODE_PROFILE_ID"] = profile.Id,
            ["CLAUDE_CODE_HARDWARE"] = FormatHardwareString(profile.Hardware)
        };

        config["env"] = env;

        return config;
    }

    private string GetModelForTier(string tier)
    {
        return tier.ToLower() switch
        {
            "efficient" => "claude-haiku-4-5",
            "balanced" => "claude-sonnet-4-6",
            "high" => "claude-sonnet-4-6",
            _ => "claude-sonnet-4-6"
        };
    }

    private string FormatHardwareString(HardwareFingerprint hardware)
    {
        if (hardware == null)
            return "unknown";

        return $"chip:{hardware.Chip},mem:{hardware.MemoryGb}GB";
    }
}
