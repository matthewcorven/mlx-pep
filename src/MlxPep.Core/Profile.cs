namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Represents a complete MLX profile with optional community metadata.
/// Issue #27: profiling: publish-flow polish + community metadata
/// </summary>
[JsonConverter(typeof(ProfileJsonConverter))]
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
    SamplerSettings? Sampler = null,
    
    [property: JsonPropertyName("community")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    CommunityMetadata? Community = null);

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
/// Represents sampler configuration (optional).
/// </summary>
public record SamplerSettings(
    [property: JsonPropertyName("type")]
    string Type,
    
    [property: JsonPropertyName("parameters")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Dictionary<string, object>? Parameters = null);

/// <summary>
/// Represents community-contributed metadata for profiles.
/// Required for publishing, optional for local-only profiles.
/// </summary>
public record CommunityMetadata(
    [property: JsonPropertyName("tags")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    List<string>? Tags = null,
    
    [property: JsonPropertyName("keywords")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    List<string>? Keywords = null,
    
    [property: JsonPropertyName("description")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Description = null,
    
    [property: JsonPropertyName("minMemoryGb")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? MinMemoryGb = null,
    
    [property: JsonPropertyName("maxMemoryGb")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? MaxMemoryGb = null,
    
    [property: JsonPropertyName("hardwareFamily")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? HardwareFamily = null,
    
    [property: JsonPropertyName("dedupKey")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? DedupKey = null);
