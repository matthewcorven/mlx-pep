# PR #64 Code Changes Review — Morpheus (Lead)

**Date**: 2026-08-13T16:45:00Z
**Reviewer**: Morpheus (Technical Lead)
**PR**: #64 Doctor Command Implementation
**Branch**: pr-64-review
**Commit**: 85fe748 (`Fix PR #64 critical blockers`)

---

## FILE-BY-FILE ANALYSIS

### ✅ src/MlxPep.Cli/Commands/DoctorCommand.cs (NEW)

**Purpose**: CLI handler for `mlx-pep doctor` command

**Key Changes**:
```csharp
public class DoctorCommand
{
    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        try
        {
            var detector = new DependencyDetectionService();
            var report = await detector.DetectAsync();

            if (context.JsonOutput)
            {
                var json = FormatAsJson(report);
                Console.WriteLine(json);  // Direct output, no wrapper
            }
            else
            {
                var table = FormatAsTable(report);
                Console.WriteLine(table);
            }

            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            return CommandResult.Failure($"Doctor check failed: {ex.Message}");
        }
    }
}
```

**Architectural Notes**:
- ✅ **Single Responsibility**: Orchestrates detection and formatting only
- ✅ **Direct Console Output**: Bypasses CliBuilder wrapper (solves BLOCKER #2)
- ✅ **Proper Error Handling**: try-catch returns CommandResult
- ✅ **Two Output Formats**: Table (human) and JSON (machine)
- ✅ **Integration**: Instantiates DependencyDetectionService (solves BLOCKER #3)

**FormatAsJson Implementation**:
- Creates anonymous object with command, timestamp, dependencies
- Serializes with indentation and null-ignore option
- Each dependency includes: installed, version, message, install guidance
- ✅ Result: Single valid JSON wrapper (BLOCKER #2 fixed)

**FormatAsTable Implementation**:
- Header: "mlx-pep doctor - Dependency Check"
- For each tool (sorted alphabetically):
  - Status symbol (✓ or ✗)
  - Display name (20 chars wide)
  - Version if installed, "not installed" message if not
  - Installation guidance (indented ℹ️)
- Footer: Summary count and suggestion to use --json

**Quality Assessment**:
- ✅ No hardcoded version parsing logic (BLOCKER #3 fixed)
- ✅ Clear separation between detection and formatting
- ✅ Follows CLI command pattern established in codebase
- ✅ Proper resource cleanup (no open handles)

---

### ✅ src/MlxPep.Core/Diagnostics/DependencyDetectionService.cs (NEW)

**Purpose**: Core service for detecting system dependencies

**Key Components**:
1. **Dictionary<string, IDependencyProbe> _probes** - Testable probe injection
2. **DetectAsync()** - Main orchestrator method
3. **DetectX() methods** - Individual tool detection (8 tools)

**Architectural Strengths**:
- ✅ **Probe Pattern**: Each tool has dedicated probe
- ✅ **Testability**: Constructor overload accepts custom probe dictionary
- ✅ **Error Resilience**: try-catch on overall detection, continue on individual tool errors
- ✅ **Structured Results**: Returns DependencyReport with all metadata
- ✅ **Safe Execution**: ArgumentList API prevents command injection

**Example: DetectDotnetAsync()**
```csharp
private async Task<ToolStatus> DetectDotnetAsync()
{
    var result = new ToolStatus { Name = "dotnet", DisplayName = ".NET" };
    
    if (!_probes.TryGetValue("dotnet", out var probe))
        return result;  // Graceful fallback
    
    var probeResult = await probe.ProbeAsync();
    
    if (!probeResult.Found)
    {
        result.Installed = false;
        result.Message = probeResult.Error ?? "Not found in PATH";
        return result;
    }
    
    result.Installed = true;
    result.Version = probe.ParseVersion(probeResult.RawOutput ?? "");
    result.Scope = DetectScope("dotnet");
    result.InstallGuidance = DependencyInstallationGuidance.GetGuidance("dotnet");
    return result;
}
```

**Tools Detected** (8 total):
1. dotnet → via DotnetProbe
2. hf-cli → via HuggingFaceCliProbe
3. python3 → via Python3Probe
4. model-assessor → via pip (Python package)
5. omlx → via OmlxProbe (app + server check)
6. vscode → via VsCodeProbe
7. vscode-insiders → via VsCodeProbe
8. copilot-cli → via CopilotCliProbe

**Quality Assessment**:
- ✅ Proper separation of detection logic from parsing
- ✅ Safe process execution with timeouts
- ✅ Handles edge cases (missing probes, process timeouts)
- ✅ Comprehensive error reporting

---

### ✅ src/MlxPep.Core/Diagnostics/Probes/SystemProcessProbe.cs (NEW)

**Purpose**: Base class for CLI-based dependency probes

**Key Features**:
```csharp
public class SystemProcessProbe : IDependencyProbe
{
    public async Task<ProbeResult> ProbeAsync()
    {
        using var process = new Process { ... };
        if (!process.Start()) return ProbeResult { Found = false };
        if (!await Task.Run(() => process.WaitForExit(...))) 
        {
            try { process.Kill(); } catch { }
            return ProbeResult { Found = false };
        }
        // ... error checking and output reading
    }
    
    public virtual string? ParseVersion(string rawOutput)
    {
        return string.IsNullOrWhiteSpace(rawOutput) 
            ? null 
            : rawOutput.Split('\n')[0].Trim();
    }
}
```

**Version Parsing Implementations**:

1. **HuggingFaceCliProbe**: 
   ```csharp
   Regex.Match(rawOutput, @"(\d+\.\d+(?:\.\d+)?(?:-[a-zA-Z0-9]+)?)")
   // Handles: 0.19.0, 0.19, 0.19.0-alpha
   ```

2. **Python3Probe**:
   ```csharp
   Regex.Match(rawOutput, @"(\d+\.\d+(?:\.\d+)?(?:-[a-zA-Z0-9]+)?)")
   // Handles: 3.11.0, 3.11, 3.11.0-dev
   ```

3. **CopilotCliProbe**:
   ```csharp
   Regex.Match(rawOutput, @"(\d+\.\d+(?:\.\d+)?(?:-[a-zA-Z0-9]+)?)")
   .TrimEnd('.')
   // Handles: 1.0.79, 1.0.79-alpha, 1.0.79.
   ```

**Quality Assessment**:
- ✅ **BLOCKER #3 FIXED**: Proper semantic version extraction
- ✅ Handles prerelease versions (e.g., 1.0.79-alpha)
- ✅ Trims trailing dots
- ✅ Uses nullable return (null if parsing fails)
- ✅ Process timeout protection (5s default)
- ✅ Safe process cleanup on timeout

---

### ✅ src/MlxPep.Core/Diagnostics/Probes/FileSystemProbe.cs (NEW)

**Purpose**: File system and environment variable probes

**Probes Included**:
1. **FileSystemProbe**: Check for file/directory existence
2. **PathProbe**: Use `which`/`where` to locate executables
3. **EnvironmentVariableProbe**: Check environment variables
4. **VsCodeProbe**: Check for app bundle + CLI
5. **OmlxProbe**: Check for app bundle + server on :8000

**Example: VsCodeProbe**
```csharp
public async Task<ProbeResult> ProbeAsync()
{
    // Check macOS app bundle first
    if (Directory.Exists("/Applications/Visual Studio Code.app"))
        return new ProbeResult { Found = true, RawOutput = appPath };
    
    // Fallback to CLI on PATH
    var pathProbe = new PathProbe("code");
    return await pathProbe.ProbeAsync();
}
```

**Example: OmlxProbe**
```csharp
public async Task<ProbeResult> ProbeAsync()
{
    // Check app bundle
    if (Directory.Exists("/Applications/oMLX.app"))
        return new ProbeResult { Found = true, RawOutput = appPath };
    
    // Check running server on :8000
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var response = await client.GetAsync("http://localhost:8000/health");
        if (response.IsSuccessStatusCode)
            return new ProbeResult { Found = true, RawOutput = "localhost:8000" };
    }
    catch { }
    
    return new ProbeResult { Found = false };
}
```

**Quality Assessment**:
- ✅ Safe HTTP probe with timeout
- ✅ Proper exception handling
- ✅ Cross-platform awareness (app bundles on macOS)
- ✅ Comprehensive coverage (app + server for oMLX)

---

### ✅ src/MlxPep.Core/Diagnostics/DependencyReport.cs (NEW)

**Purpose**: Result structure for dependency detection

**Structure**:
```csharp
public class DependencyReport
{
    public Dictionary<string, ToolStatus> Tools { get; } = new();
    public DependencyReportStatus Status { get; set; }
    public List<string> Warnings { get; } = new();
}

public enum DependencyReportStatus
{
    Success,
    PartialSuccess,
    Failed
}

public class ToolStatus
{
    public string Name { get; set; }  // Internal identifier (dotnet, hf-cli)
    public string DisplayName { get; set; }  // Human-readable (Hugging Face CLI)
    public bool Installed { get; set; }
    public string? Version { get; set; }
    public string? Message { get; set; }
    public string? Scope { get; set; }  // user/global/unknown
    public string? RawOutput { get; set; }
    public string? ToolPath { get; set; }
    public string? InstallGuidance { get; set; }
}
```

**Quality Assessment**:
- ✅ Comprehensive status tracking
- ✅ Separation of internal names (Name) from display names (DisplayName)
- ✅ Supports scope detection (user vs global install)
- ✅ Stores raw probe output for debugging

---

### ✅ src/MlxPep.Core/Diagnostics/IDependencyProbe.cs (NEW)

**Purpose**: Probe interface for testability

**Contract**:
```csharp
public interface IDependencyProbe
{
    Task<ProbeResult> ProbeAsync();
    string? ParseVersion(string rawOutput);
}

public class ProbeResult
{
    public bool Found { get; set; }
    public string? RawOutput { get; set; }
    public string? Error { get; set; }
}
```

**Quality Assessment**:
- ✅ Simple, testable contract
- ✅ Async support for long operations
- ✅ Structured result type
- ✅ Error propagation

---

### ✅ src/MlxPep.Core/Diagnostics/DependencyInstallationGuidance.cs (NEW)

**Purpose**: Centralized installation instructions

**Example**:
```csharp
public static class DependencyInstallationGuidance
{
    private static readonly Dictionary<string, string> Guidance = new()
    {
        ["dotnet"] = "Install .NET 10.0:\n  macOS: brew install dotnet\n  Or download from: https://...",
        ["python3"] = "Install Python 3:\n  macOS: brew install python3\n  Or download from: https://...",
        ["hf-cli"] = "Install Hugging Face CLI:\n  pip install huggingface-hub\n  Then run: huggingface-cli login",
        // ... 8 tools total
    };
    
    public static string GetGuidance(string toolName) 
        => Guidance.TryGetValue(toolName, out var g) ? g : "No installation guidance available";
}
```

**Quality Assessment**:
- ✅ Centralized guidance (single source of truth)
- ✅ Platform-specific instructions (brew for macOS)
- ✅ Links to official resources
- ✅ Token safety: guidance avoids hardcoding secrets

---

### ✅ tests/MlxPep.Cli.Tests/DoctorCommandTests.cs (NEW)

**Purpose**: Comprehensive testing for DoctorCommand

**Test Coverage** (9 tests):
1. `ExecuteAsync_WithJsonFlag_OutputsValidJson` - JSON valid and parseable
2. `ExecuteAsync_WithoutJsonFlag_OutputsTable` - Table format works
3. `ExecuteAsync_ReturnsSuccess` - Exit code 0
4. `ExecuteAsync_JsonOutput_IncludesAllDependencies` - All 8 tools present
5. `ExecuteAsync_TableOutput_DisplaysCorrectNames` - Human-readable names shown
6. `ExecuteAsync_TableOutput_ShowsInstallationGuidance` - Guidance displayed
7. `ExecuteAsync_JsonOutput_ValidStructure` - JSON has required fields
8. `ExecuteAsync_JsonOutput_DependencyHasCorrectFields` - Dependency objects valid
9. `ExecuteAsync_TableOutput_ContainsStatusSymbols` - ✓ and ✗ symbols present

**Test Pattern**:
```csharp
[Fact]
public async Task ExecuteAsync_WithJsonFlag_OutputsValidJson()
{
    // Arrange
    var command = new DoctorCommand();
    var context = new CommandContext { JsonOutput = true };
    var oldOutput = Console.Out;
    using (var writer = new StringWriter())
    {
        Console.SetOut(writer);
        
        // Act
        var result = await command.ExecuteAsync(context);
        Console.SetOut(oldOutput);
        var output = writer.ToString();
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        var json = JsonDocument.Parse(output);  // ✅ Validates JSON
        Assert.NotNull(json);
        // ... field validation
    }
}
```

**Quality Assessment**:
- ✅ All tests passing (9/9)
- ✅ Comprehensive coverage (JSON, table, exit codes, fields)
- ✅ Proper StringWriter for testing Console output
- ✅ Uses JsonDocument for validation (not string matching)
- ✅ Tests for all 8 dependencies present

---

### ✅ tests/MlxPep.Cli.Tests/UnitTest1.cs (DELETED)

**Change**: Stub test file removed
- **Before**: `public class UnitTest1 { [Fact] public void Test1() { } }`
- **After**: FILE DELETED

**Impact**:
- ✅ BLOCKER #1 FIXED: Removes test clutter
- ✅ Improves project hygiene
- ✅ No functionality lost (empty stub)

---

### ✅ src/MlxPep.Cli/CliBuilder.cs (MODIFIED)

**Changes**: Removed 9 lines (cleanup)

**Before**:
- Was wrapping DoctorCommand output with JSON wrapper
- Created inconsistent output handling

**After**:
- DoctorCommand handles own output (direct Console.WriteLine)
- CliBuilder only dispatches to command
- No output manipulation by framework

**Result**:
- ✅ BLOCKER #2 FIXED: No double JSON wrapper

---

## VERIFICATION SUMMARY

### ✅ All 3 Critical Blockers Resolved

| Blocker | Issue | Fix | Verification |
|---------|-------|-----|--------------|
| **#1** | Stub test clutter | Delete UnitTest1.cs | File not in HEAD ✅ |
| **#2** | JSON double-wrap | Direct Console.WriteLine | JSON valid, single object ✅ |
| **#3** | Version parsing bug | Integrated DependencyDetectionService | Regex patterns correct ✅ |

### ✅ Architecture Compliance

- **Issue #11**: DependencyDetectionService properly implemented
- **Probe Pattern**: Clear abstraction for testability
- **CLI Pattern**: Consistent with existing commands
- **Error Handling**: Comprehensive with proper timeouts

### ✅ Code Quality

- **Build**: 0 errors, 0 warnings
- **Tests**: 197 passed, 9 new tests for doctor
- **Documentation**: XML comments on public classes
- **Naming**: Clear, consistent naming convention
- **Structure**: Proper separation of concerns

---

## ARCHITECTURAL WINS

1. **Probe-Based Design**: Enables testing with mock probes
2. **Structured Results**: DependencyReport carries all metadata
3. **Centralized Guidance**: DependencyInstallationGuidance single source of truth
4. **Safe Execution**: ArgumentList API prevents command injection
5. **Two Output Formats**: Same detection logic, different formatting

---

## MORPHEUS VERDICT

### 🟢 APPROVED FOR MERGE

All code changes reviewed and verified. No architectural violations. All critical blockers resolved. Architecture aligns with Issue #11. Tests pass comprehensively.

**Recommendation**: Merge immediately. Dual-gate convergence achieved (Rai 70% + Morpheus 85%).

---

**Signature**: Morpheus (Lead)
**Date**: 2026-08-13T16:45:00Z
