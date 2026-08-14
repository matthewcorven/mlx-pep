namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Builds harness-ready apply profiles from a full assessed profile set.
/// Aggregates tiered profiles where the target harness can represent multiple named entries.
/// </summary>
public static class HarnessProfileSetBuilder
{
    public static Profile BuildApplyProfile(List<Profile> profiles, string harness)
    {
        if (profiles == null || profiles.Count == 0)
            throw new ArgumentException("Profiles list cannot be empty", nameof(profiles));

        var balanced = RequireProfile(profiles, "balanced");

        return harness switch
        {
            "vscode" => BuildVscodeAggregateProfile(profiles, balanced, RequireProfile(profiles, "efficient")),
            "copilot-cli" => BuildCopilotAggregateProfile(
                profiles,
                balanced,
                RequireProfile(profiles, "efficient"),
                RequireProfile(profiles, "high")),
            "opencode" => balanced,
            "claude-code" => balanced,
            _ => balanced
        };
    }

    private static Profile RequireProfile(List<Profile> profiles, string tier)
    {
        return profiles.FirstOrDefault(profile =>
            profile.Tier.Equals(tier, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Profile set is missing required tier '{tier}' for harness aggregation.");
    }

    private static Profile BuildVscodeAggregateProfile(List<Profile> profiles, Profile balanced, Profile efficient)
    {
        var aggregatedModels = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in profiles)
        {
            if (profile.Harness.TryGetValue("vscode", out var vscodeObj) &&
                JsonValueConverter.AsDictionary(vscodeObj) is Dictionary<string, object> vscodeDict &&
                vscodeDict.TryGetValue("chatLanguageModels", out var chatModelsObj) &&
                JsonValueConverter.AsDictionary(chatModelsObj) is Dictionary<string, object> chatModelsDict &&
                chatModelsDict.TryGetValue("models", out var modelsObj) &&
                JsonValueConverter.AsDictionary(modelsObj) is Dictionary<string, object> modelsDict)
            {
                foreach (var (key, value) in modelsDict)
                {
                    aggregatedModels[key] = value;
                }
            }
        }

        if (aggregatedModels.Count == 0)
            throw new InvalidOperationException("Profile set does not contain any harness.vscode.chatLanguageModels entries to aggregate.");

        var customSettings = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["inlineChat.defaultModel"] = balanced.Id,
            ["chat.utilityModel"] = balanced.Id,
            ["chat.utilitySmallModel"] = efficient.Id
        };

        var harness = CloneHarnessDictionary(balanced.Harness);
        harness["vscode"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["maxInputTokens"] = GetNestedInt(balanced.Harness, "vscode", "maxInputTokens"),
            ["maxOutputTokens"] = GetNestedInt(balanced.Harness, "vscode", "maxOutputTokens"),
            ["customSettings"] = customSettings,
            ["chatLanguageModels"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["models"] = aggregatedModels
            }
        };

        return balanced with
        {
            Id = $"{balanced.ModelHfId.Split('/').Last()}-vscode-bundle",
            Harness = harness
        };
    }

    private static Profile BuildCopilotAggregateProfile(List<Profile> profiles, Profile balanced, Profile efficient, Profile high)
    {
        var aggregatedProfiles = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in profiles)
        {
            if (profile.Harness.TryGetValue("copilotCli", out var copilotObj) &&
                JsonValueConverter.AsDictionary(copilotObj) is Dictionary<string, object> copilotDict)
            {
                aggregatedProfiles[profile.Id] = new Dictionary<string, object>(copilotDict, StringComparer.OrdinalIgnoreCase);
            }
        }

        if (aggregatedProfiles.Count == 0)
            throw new InvalidOperationException("Profile set does not contain any harness.copilotCli entries to aggregate.");

        var harness = CloneHarnessDictionary(balanced.Harness);
        harness["copilotCli"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["profiles"] = aggregatedProfiles,
            ["defaultProfile"] = balanced.Id
        };

        return balanced with
        {
            Id = $"{balanced.ModelHfId.Split('/').Last()}-copilot-bundle",
            Harness = harness
        };
    }

    private static Dictionary<string, object> CloneHarnessDictionary(Dictionary<string, object> harness)
    {
        var clone = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in harness)
        {
            clone[key] = value;
        }

        return clone;
    }

    private static int GetNestedInt(Dictionary<string, object> harness, string section, string key)
    {
        if (harness.TryGetValue(section, out var sectionObj) &&
            JsonValueConverter.AsDictionary(sectionObj) is Dictionary<string, object> sectionDict &&
            sectionDict.TryGetValue(key, out var value) &&
            JsonValueConverter.AsInt(value) is int intValue)
        {
            return intValue;
        }

        return 0;
    }
}