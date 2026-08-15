namespace MlxPep.Core.Profiling;

using System;

/// <summary>
/// Represents the managed result of a model-assessor profiling run.
/// </summary>
public record AssessmentRunResult(
    string OperationId,
    string RunId,
    string ModelId,
    string Status,
    string Suite,
    string MtpMode,
    string CreatedAt,
    RecommendationManifest RecommendationManifest)
{
    public bool IsSuccess => Status == "success";
}
