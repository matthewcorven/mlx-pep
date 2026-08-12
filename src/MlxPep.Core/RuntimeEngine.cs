namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Interface for runtime engine handlers.
/// Supports profiling and running via different inference engines.
/// Issue #25: runtimes: mlx-lm / llama.cpp / vLLM support
/// </summary>
public interface IRuntimeEngine
{
    /// <summary>
    /// Engine identifier (e.g., "omlx", "mlx-lm", "llama.cpp", "vllm").
    /// </summary>
    string EngineId { get; }

    /// <summary>
    /// Validates engine-specific settings in the profile.
    /// </summary>
    ValidationResult ValidateSettings(Profile profile);

    /// <summary>
    /// Gets engine-specific metadata for discovery/search.
    /// </summary>
    Dictionary<string, object> GetMetadata();
}

/// <summary>
/// Base class for runtime engine implementations.
/// </summary>
public abstract class RuntimeEngineBase : IRuntimeEngine
{
    public abstract string EngineId { get; }

    public virtual ValidationResult ValidateSettings(Profile profile)
    {
        var errors = new List<string>();

        if (!profile.Engine.Equals(EngineId, StringComparison.OrdinalIgnoreCase))
            errors.Add($"Profile engine '{profile.Engine}' does not match handler engine '{EngineId}'.");

        return errors.Any()
            ? new ValidationResult(false, errors)
            : new ValidationResult(true, new List<string>());
    }

    public virtual Dictionary<string, object> GetMetadata()
    {
        return new Dictionary<string, object>
        {
            { "engine", EngineId },
            { "type", GetType().Name }
        };
    }
}

/// <summary>
/// Handler for oMLX (MLX Framework) runtime.
/// </summary>
public class OMLXEngine : RuntimeEngineBase
{
    public override string EngineId => "omlx";

    public override ValidationResult ValidateSettings(Profile profile)
    {
        var baseResult = base.ValidateSettings(profile);
        if (!baseResult.IsValid)
            return baseResult;

        var errors = new List<string>();

        // Require OMLXSettings for omlx engine
        if (profile.OMLXSettings == null || !profile.OMLXSettings.Any())
            errors.Add("OMLXSettings are required for omlx engine.");

        return errors.Any()
            ? new ValidationResult(false, errors)
            : new ValidationResult(true, new List<string>());
    }

    public override Dictionary<string, object> GetMetadata()
    {
        var metadata = base.GetMetadata();
        metadata["description"] = "Optimized MLX Framework runtime";
        metadata["framework"] = "MLX";
        return metadata;
    }
}

/// <summary>
/// Handler for mlx-lm runtime (MLX community models).
/// </summary>
public class MlxLmEngine : RuntimeEngineBase
{
    public override string EngineId => "mlx-lm";

    public override ValidationResult ValidateSettings(Profile profile)
    {
        var baseResult = base.ValidateSettings(profile);
        return baseResult;
    }

    public override Dictionary<string, object> GetMetadata()
    {
        var metadata = base.GetMetadata();
        metadata["description"] = "MLX Community Models (mlx-lm)";
        metadata["framework"] = "MLX";
        metadata["scope"] = "community";
        return metadata;
    }
}

/// <summary>
/// Handler for llama.cpp runtime.
/// </summary>
public class LlamaCppEngine : RuntimeEngineBase
{
    public override string EngineId => "llama.cpp";

    public override ValidationResult ValidateSettings(Profile profile)
    {
        var baseResult = base.ValidateSettings(profile);
        return baseResult;
    }

    public override Dictionary<string, object> GetMetadata()
    {
        var metadata = base.GetMetadata();
        metadata["description"] = "llama.cpp: Efficient inference for Llama models";
        metadata["framework"] = "llama.cpp";
        metadata["portable"] = true;
        return metadata;
    }
}

/// <summary>
/// Handler for vLLM runtime.
/// </summary>
public class VllmEngine : RuntimeEngineBase
{
    public override string EngineId => "vllm";

    public override ValidationResult ValidateSettings(Profile profile)
    {
        var baseResult = base.ValidateSettings(profile);
        return baseResult;
    }

    public override Dictionary<string, object> GetMetadata()
    {
        var metadata = base.GetMetadata();
        metadata["description"] = "vLLM: High-throughput LLM inference engine";
        metadata["framework"] = "vLLM";
        metadata["optimized_for"] = "throughput";
        return metadata;
    }
}

/// <summary>
/// Registry for runtime engine handlers.
/// Provides engine instances by ID and enables validation across all supported engines.
/// </summary>
public class RuntimeEngineRegistry
{
    private readonly Dictionary<string, IRuntimeEngine> _engines;

    public RuntimeEngineRegistry()
    {
        var omlxEngine = new OMLXEngine();
        var mlxLmEngine = new MlxLmEngine();
        var llamaCppEngine = new LlamaCppEngine();
        var vllmEngine = new VllmEngine();

        _engines = new Dictionary<string, IRuntimeEngine>(StringComparer.OrdinalIgnoreCase)
        {
            { "omlx", omlxEngine },
            { "mlx", omlxEngine },  // Alias for backward compatibility
            { "mlx-lm", mlxLmEngine },
            { "llama.cpp", llamaCppEngine },
            { "vllm", vllmEngine }
        };
    }

    /// <summary>
    /// Gets a registered runtime engine by ID.
    /// </summary>
    public IRuntimeEngine? GetEngine(string engineId)
    {
        return _engines.TryGetValue(engineId, out var engine) ? engine : null;
    }

    /// <summary>
    /// Gets all registered engine IDs.
    /// </summary>
    public IEnumerable<string> GetEngineIds()
    {
        return _engines.Keys;
    }

    /// <summary>
    /// Checks if an engine is registered.
    /// </summary>
    public bool IsSupported(string engineId)
    {
        return _engines.ContainsKey(engineId);
    }

    /// <summary>
    /// Validates profile settings using engine-specific handler.
    /// </summary>
    public ValidationResult ValidateProfileForEngine(Profile profile)
    {
        var engine = GetEngine(profile.Engine);
        if (engine == null)
            return new ValidationResult(false, new List<string> { $"Unknown engine: {profile.Engine}" });

        // Normalize profile engine ID to the canonical engine ID (e.g., "mlx" → "omlx")
        var normalizedProfile = profile with { Engine = engine.EngineId };
        return engine.ValidateSettings(normalizedProfile);
    }

    /// <summary>
    /// Gets metadata for all registered engines.
    /// </summary>
    public Dictionary<string, Dictionary<string, object>> GetAllEngineMetadata()
    {
        return _engines.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetMetadata()
        );
    }
}
