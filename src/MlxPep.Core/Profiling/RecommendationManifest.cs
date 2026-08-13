namespace MlxPep.Core.Profiling;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Represents the complete recommendation manifest output from model-assessor.
/// This is the stable JSON contract between the CLI and the Python profiling pipeline.
/// </summary>
public record RecommendationManifest(
    [property: JsonPropertyName("modelHfId")]
    string ModelHfId,

    [property: JsonPropertyName("assessmentVersion")]
    string AssessmentVersion,

    [property: JsonPropertyName("timestamp")]
    string Timestamp,

    [property: JsonPropertyName("recommendations")]
    Dictionary<string, TierRecommendation> Recommendations,

    [property: JsonPropertyName("assessmentNotes")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? AssessmentNotes = null,

    [property: JsonPropertyName("hardware")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    HardwareAssessment? Hardware = null);

/// <summary>
/// Represents a single tier's recommendation from model-assessor.
/// </summary>
public record TierRecommendation(
    [property: JsonPropertyName("tier")]
    string Tier,

    [property: JsonPropertyName("system")]
    Dictionary<string, object> System,

    [property: JsonPropertyName("omlx")]
    Dictionary<string, object> Omlx,

    [property: JsonPropertyName("harness")]
    Dictionary<string, object> Harness,

    [property: JsonPropertyName("sampler")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Dictionary<string, object>? Sampler = null,

    [property: JsonPropertyName("rationale")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Rationale = null,

    [property: JsonPropertyName("expectedTokensPerSecond")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? ExpectedTokensPerSecond = null,

    [property: JsonPropertyName("expectedMemoryGb")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? ExpectedMemoryGb = null);

/// <summary>
/// Optional hardware assessment metadata from model-assessor.
/// </summary>
public record HardwareAssessment(
    [property: JsonPropertyName("chip")]
    string Chip,

    [property: JsonPropertyName("memoryGb")]
    int MemoryGb,

    [property: JsonPropertyName("modelIdentifier")]
    string ModelIdentifier,

    [property: JsonPropertyName("osVersion")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? OsVersion = null);
