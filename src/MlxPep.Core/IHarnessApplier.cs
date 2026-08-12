namespace MlxPep.Core;

using System.Threading.Tasks;

/// <summary>
/// Handler for a single harness type.
/// Issue #16: harness apply profile to Copilot CLI + VS Code/Insiders
/// </summary>
public interface IHarnessApplier
{
    string HarnessName { get; }
    
    /// <summary>
    /// Applies a profile to the harness.
    /// </summary>
    /// <param name="profile">The profile to apply</param>
    /// <param name="isDryRun">If true, computes changes but does not write</param>
    /// <param name="requestedInsiders">For vscode harness: apply to Insiders instead of stable</param>
    /// <returns>Result with changes and backup location</returns>
    Task<HarnessApplyResult> ApplyAsync(
        Profile profile,
        bool isDryRun = false,
        bool requestedInsiders = false);
}
