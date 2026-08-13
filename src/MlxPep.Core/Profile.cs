namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Represents a complete MLX profile for a specific model and tier.
/// Issue #8: core: profile schema records + STJ source-gen + JSONL validation
/// </summary>
public record Profile(
    [property: JsonPropertyName("schemaVersion")]
    int SchemaVersion,

    [property: JsonPropertyName("id")]
    string Id,

    [property: JsonPropertyName("modelHfId")]
    string ModelHfId,

    [property: JsonPropertyName("tier")]
    string Tier,

    [property: JsonPropertyName("engine")]
    string Engine,

    [property: JsonPropertyName("system")]
    Dictionary<string, object> System,

    [property: JsonPropertyName("omlx")]
    Dictionary<string, object> OMLXSettings,

    [property: JsonPropertyName("harness")]
    Dictionary<string, object> Harness,

    [property: JsonPropertyName("provenance")]
    ProfileProvenance Provenance,

    [property: JsonPropertyName("hardware")]
    HardwareFingerprint Hardware,

    [property: JsonPropertyName("sampler")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    SamplerSettings? Sampler = null);

/// <summary>
/// Represents profile origin and creation metadata.
/// </summary>
public record ProfileProvenance(
    [property: JsonPropertyName("author")]
    string Author,

    [property: JsonPropertyName("createdAt")]
    string CreatedAt,

    [property: JsonPropertyName("source")]
    string Source);

/// <summary>
/// Represents hardware configuration metadata.
/// </summary>
public record HardwareFingerprint(
    [property: JsonPropertyName("chip")]
    string Chip,

    [property: JsonPropertyName("memoryGb")]
    int MemoryGb,

    [property: JsonPropertyName("modelIdentifier")]
    string ModelIdentifier);

/// <summary>
/// Represents sampler configuration (optional) with direct fields.
/// </summary>
public record SamplerSettings(
    [property: JsonPropertyName("temperature")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? Temperature = null,

    [property: JsonPropertyName("topP")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? TopP = null,

    [property: JsonPropertyName("topK")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? TopK = null,

    [property: JsonPropertyName("repetitionPenalty")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? RepetitionPenalty = null,

    [property: JsonPropertyName("contextTokens")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? ContextTokens = null);


