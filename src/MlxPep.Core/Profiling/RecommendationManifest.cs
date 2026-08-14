namespace MlxPep.Core.Profiling;

using System.Collections.Generic;

/// <summary>
/// DTO for recommendation manifest returned by model-assessor.
/// Represents tiered recommendations for a model across different performance profiles.
/// </summary>
public record RecommendationManifest(
    string ModelHfId,
    string AssessmentVersion,
    string Timestamp,
    Dictionary<string, TierRecommendation> Recommendations,
    HardwareAssessment? Hardware = null);

/// <summary>
/// Recommendation for a specific performance tier (high, balanced, efficient).
/// </summary>
public record TierRecommendation(
    string Tier,
    Dictionary<string, object> System,
    Dictionary<string, object> Omlx,
    Dictionary<string, object> Harness,
    Dictionary<string, object>? Sampler = null);

/// <summary>
/// Hardware assessment from model-assessor.
/// </summary>
public record HardwareAssessment(
    string Chip,
    int MemoryGb,
    string ModelIdentifier);
