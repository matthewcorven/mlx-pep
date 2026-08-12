namespace MlxPep.Core.Emitters;

/// <summary>
/// Defines contract for emitting harness-specific configurations from profiles.
/// Issue #24: harness: OpenCode + Claude Code emitters
/// </summary>
public interface IHarnessEmitter
{
    /// <summary>
    /// Emits a profile to the target harness format (JSON string).
    /// </summary>
    /// <param name="profile">The profile to emit</param>
    /// <returns>JSON string in the target format</returns>
    Task<string> EmitAsync(Profile profile);

    /// <summary>
    /// Gets the target filename for this emitter's output.
    /// </summary>
    /// <returns>Filename (e.g. "opencode.json" or "settings.json")</returns>
    string GetTargetFileName();

    /// <summary>
    /// Validates a profile has the required fields for this emitter.
    /// </summary>
    /// <param name="profile">Profile to validate</param>
    /// <returns>List of validation error messages (empty if valid)</returns>
    List<string> Validate(Profile profile);
}
