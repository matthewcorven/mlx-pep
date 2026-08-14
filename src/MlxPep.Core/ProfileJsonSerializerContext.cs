namespace MlxPep.Core;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using MlxPep.Core.Profiling;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = false)]
[JsonSerializable(typeof(Profile))]
[JsonSerializable(typeof(ProfileProvenance))]
[JsonSerializable(typeof(HardwareFingerprint))]
[JsonSerializable(typeof(SamplerSettings))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(List<object>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(List<Profile>))]
[JsonSerializable(typeof(RecommendationManifest))]
[JsonSerializable(typeof(TierRecommendation))]
[JsonSerializable(typeof(HardwareAssessment))]
public partial class ProfileJsonSerializerContext : JsonSerializerContext
{
}
