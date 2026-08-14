namespace MlxPep.Core.Detectors;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Immutable result of system hardware detection.
/// Issue #10: core: system + oMLX read-only detectors
/// </summary>
public record SystemHardwareInfo(
    [property: JsonPropertyName("modelName")]
    string ModelName,

    [property: JsonPropertyName("modelIdentifier")]
    string ModelIdentifier,

    [property: JsonPropertyName("chip")]
    string Chip,

    [property: JsonPropertyName("memoryGb")]
    int MemoryGb,

    [property: JsonPropertyName("storageFreeGb")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? StorageFreeGb,

    [property: JsonPropertyName("storageCapacityTb")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? StorageCapacityTb,

    [property: JsonPropertyName("wiredLimitMb")]
    int WiredLimitMb);

/// <summary>
/// Immutable result of oMLX state detection.
/// Issue #10: core: system + oMLX read-only detectors
/// </summary>
public record OmlxState(
    [property: JsonPropertyName("configPath")]
    string ConfigPath,

    [property: JsonPropertyName("logPath")]
    string LogPath,

    [property: JsonPropertyName("basePath")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? BasePath,

    [property: JsonPropertyName("port")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Port,

    [property: JsonPropertyName("modelDir")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ModelDir,

    [property: JsonPropertyName("currentMemoryGuardTier")]
    string CurrentMemoryGuardTier,

    [property: JsonPropertyName("currentCeilingGb")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? CurrentCeilingGb,

    [property: JsonPropertyName("currentMetalCapGb")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? CurrentMetalCapGb,

    [property: JsonPropertyName("recommendedWiredLimitMb")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? RecommendedWiredLimitMb);

/// <summary>
/// Combined detection results from both SystemDetector and OmlxDetector.
/// Used for profiling discovery and hardware metadata population.
/// </summary>
public record DetectionResults(
    [property: JsonPropertyName("hardware")]
    SystemHardwareInfo Hardware,

    [property: JsonPropertyName("omlx")]
    OmlxState Omlx,

    [property: JsonPropertyName("timestamp")]
    string Timestamp);
