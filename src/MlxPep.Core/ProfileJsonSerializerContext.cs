namespace MlxPep.Core;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = false)]
[JsonSerializable(typeof(Profile))]
[JsonSerializable(typeof(ProfileProvenance))]
[JsonSerializable(typeof(HardwareFingerprint))]
[JsonSerializable(typeof(SamplerSettings))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(List<Profile>))]
public partial class ProfileJsonSerializerContext : JsonSerializerContext
{
}
