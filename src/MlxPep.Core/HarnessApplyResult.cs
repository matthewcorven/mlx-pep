namespace MlxPep.Core;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents the result of an apply operation (dry-run or real).
/// Issue #16: harness apply profile to Copilot CLI + VS Code/Insiders
/// </summary>
public record HarnessApplyResult(
    string ProfileId,
    string Harness,  // "vscode" | "copilot-cli"
    bool IsDryRun,
    bool Success,
    string? Error = null,
    List<FileChangeResult> Changes = null!,
    string? BackupLocation = null);

/// <summary>
/// Represents a single file change.
/// </summary>
public record FileChangeResult(
    string FilePath,
    string Status,  // "new" | "modified" | "unchanged"
    string? ExistingContent = null,
    string? ProposedContent = null,
    string? DiffOutput = null);
