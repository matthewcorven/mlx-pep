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
        var harnessConfig = profile.Harness?.TryGetValue("opencode", out var harnessObj) == true &&
            JsonValueConverter.AsDictionary(harnessObj) is Dictionary<string, object> harnessDict
            ? harnessDict
            : new Dictionary<string, object>();

        var providerId = GetStringValue(harnessConfig, "providerId") ?? "omlx-local";
        var modelId = GetStringValue(harnessConfig, "modelId") ?? profile.ModelHfId;
        var displayName = GetStringValue(harnessConfig, "displayName") ?? profile.Id;
        var baseUrl = GetStringValue(harnessConfig, "baseUrl") ?? string.Empty;
        var apiKeyEnv = GetStringValue(harnessConfig, "apiKeyEnv") ?? "OMLX_API_KEY";

        var metadata = new JsonObject
        {
            ["generatedFrom"] = profile.Id,
            ["tier"] = profile.Tier,
            ["generatedAt"] = DateTime.UtcNow.ToString("O"),
            ["modelId"] = modelId
        };

        if (profile.Hardware != null)
        {
            metadata["memoryGb"] = profile.Hardware.MemoryGb;
        }

        var config = new JsonObject
        {
            ["$schema"] = "https://opencode.ai/config.json",
            ["model"] = $"{providerId}/{profile.Id}",
            ["small_model"] = $"{providerId}/{profile.Id}",
            ["metadata"] = metadata
        };

        var providerConfig = new JsonObject
        {
            [providerId] = new JsonObject
            {
                ["npm"] = "@ai-sdk/openai-compatible",
                ["name"] = "oMLX Local",
                ["options"] = new JsonObject
                {
                    ["baseURL"] = baseUrl,
                    ["apiKey"] = $"{{env:{apiKeyEnv}}}"
                },
                ["models"] = new JsonObject
                {
                    [profile.Id] = new JsonObject
                    {
                        ["name"] = displayName,
                        ["limit"] = new JsonObject
                        {
                            ["context"] = GetIntValue(harnessConfig, "maxInputTokens"),
                            ["output"] = GetIntValue(harnessConfig, "maxOutputTokens")
                        },
                        ["metadata"] = new JsonObject
                        {
                            ["modelId"] = modelId
                        }
                    }
                }
            }
        };

        config["provider"] = providerConfig;

        // Extract opencode settings
        if (profile.Harness?.TryGetValue("opencode", out var opencodeObj) == true &&
            JsonValueConverter.AsDictionary(opencodeObj) is Dictionary<string, object> opencode)
        {
            var opencodeConfig = new JsonObject();

            var maxInputTokens = GetIntValue(opencode, "maxInputTokens");
            var maxOutputTokens = GetIntValue(opencode, "maxOutputTokens");

            if (maxInputTokens > 0)
                opencodeConfig["maxTokens"] = maxInputTokens;

            if (maxOutputTokens > 0)
                opencodeConfig["maxOutputTokens"] = maxOutputTokens;

            config["options"] = opencodeConfig;
        }

        // Extract sampler settings
        if (profile.Sampler != null)
        {
            var samplerConfig = new JsonObject();

            if (profile.Sampler.Temperature.HasValue)
                samplerConfig["temperature"] = profile.Sampler.Temperature.Value;

            if (profile.Sampler.TopP.HasValue)
                samplerConfig["topP"] = profile.Sampler.TopP.Value;

            if (samplerConfig.Count > 0)
                config["sampler"] = samplerConfig;
        }

        return config;
    }

    private static int GetIntValue(Dictionary<string, object> config, string key)
    {
        if (!config.TryGetValue(key, out var value) || value == null)
            return 0;

        return JsonValueConverter.AsInt(value) ?? 0;
    }

    private static string? GetStringValue(Dictionary<string, object> config, string key)
    {
        if (!config.TryGetValue(key, out var value) || value == null)
            return null;

        return JsonValueConverter.AsString(value);
    }
}
