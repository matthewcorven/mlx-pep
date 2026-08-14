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

        // Validate harness has claude-code config
        if (profile.Harness == null || !profile.Harness.ContainsKey("claude-code"))
        {
            errors.Add("Profile harness must contain 'claude-code' configuration for Claude Code emitter");
        }

        // Validate claude-code has maxInputTokens and maxOutputTokens
        if (profile.Harness?.TryGetValue("claude-code", out var claudeObj) == true)
        {
            if (claudeObj is Dictionary<string, object> claude)
            {
                if (!claude.ContainsKey("maxInputTokens") || !claude.ContainsKey("maxOutputTokens"))
                {
                    errors.Add("harness.claude-code must include maxInputTokens and maxOutputTokens");
                }
            }
        }

        return errors;
    }

    private JsonObject BuildClaudeCodeConfig(Profile profile)
    {
        var harnessConfig = profile.Harness?.TryGetValue("claude-code", out var harnessObj) == true &&
            JsonValueConverter.AsDictionary(harnessObj) is Dictionary<string, object> harnessDict
            ? harnessDict
            : new Dictionary<string, object>();

        var modelId = GetStringValue(harnessConfig, "modelId") ?? profile.ModelHfId;
        var displayName = GetStringValue(harnessConfig, "displayName") ?? profile.Id;
        var baseUrl = GetStringValue(harnessConfig, "baseUrl") ?? string.Empty;
        var apiKeyEnv = GetStringValue(harnessConfig, "apiKeyEnv") ?? "OMLX_API_KEY";

        var config = new JsonObject
        {
            ["model"] = modelId,
            ["metadata"] = new JsonObject
            {
                ["generatedFrom"] = profile.Id,
                ["modelId"] = modelId,
                ["displayName"] = displayName,
                ["tier"] = profile.Tier,
                ["engine"] = profile.Engine,
                ["generatedAt"] = profile.Provenance?.CreatedAt ?? DateTime.UtcNow.ToString("O")
            }
        };

        // Extract claude-code settings for limits
        if (profile.Harness?.TryGetValue("claude-code", out var claudeObj) == true &&
            claudeObj is Dictionary<string, object> claude)
        {
            var limits = new JsonObject();

            if (claude.TryGetValue("maxInputTokens", out var maxInput))
                limits["maxInputTokens"] = Convert.ToInt32(maxInput);

            if (claude.TryGetValue("maxOutputTokens", out var maxOutput))
                limits["maxOutputTokens"] = Convert.ToInt32(maxOutput);

            if (limits.Count > 0)
                config["limits"] = limits;
        }

        // Extract sampler settings for inference
        if (profile.Sampler != null)
        {
            var inference = new JsonObject();

            if (profile.Sampler.Temperature.HasValue)
                inference["temperature"] = profile.Sampler.Temperature.Value;

            if (profile.Sampler.TopP.HasValue)
                inference["topP"] = profile.Sampler.TopP.Value;

            if (profile.Sampler.TopK.HasValue)
                inference["topK"] = profile.Sampler.TopK.Value;

            if (inference.Count > 0)
                config["inference"] = inference;
        }

        // Build environment variables
        var env = new JsonObject
        {
            ["CLAUDE_CODE_TIER"] = profile.Tier,
            ["CLAUDE_CODE_PROFILE_ID"] = profile.Id,
            ["CLAUDE_CODE_HARDWARE"] = FormatHardwareString(profile.Hardware),
            ["ANTHROPIC_MODEL"] = modelId,
            ["ANTHROPIC_BASE_URL"] = baseUrl,
            ["ANTHROPIC_API_KEY"] = $"{{env:{apiKeyEnv}}}"
        };

        config["env"] = env;

        return config;
    }

    private string FormatHardwareString(HardwareFingerprint hardware)
    {
        if (hardware == null)
            return "unknown";

        return $"chip:{hardware.Chip},mem:{hardware.MemoryGb}GB";
    }

    private static string? GetStringValue(Dictionary<string, object> config, string key)
    {
        if (!config.TryGetValue(key, out var value) || value == null)
            return null;

        return JsonValueConverter.AsString(value);
    }
}
