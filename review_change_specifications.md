# RAI-Focused Adversarial Review of PR #65

## Executive Summary

PR #65 implements a dependency detection service with **1 critical RAI issue** and **5 medium-severity findings**. The architecture is sound for testability, but several security controls need hardening:

1. **CRITICAL**: Unrestricted command execution via `which`/`where` on PATH
2. Data minimization: `RawOutput` field unnecessarily exposes tool stderr
3. Path traversal: File path arguments not validated in FileSystemProbe
4. Injection vectors: Process arguments via unsafe string concatenation
5. Credential exposure: HF CLI login guidance may expose auth tokens
6. Error handling: Detailed error messages leak diagnostic paths

---

## RAI Threat Categories & Findings

### 1. CRITICAL: Command Injection via `which`/`where` Execution

**Location**: `FileSystemProbe.cs` → `PathProbe.ProbeAsync()` (lines 59-84)

**Threat**: The `PathProbe` executes `which <command>` or `where <command>` to locate executables on PATH. The `_command` parameter is passed directly as the second argument without sanitization:

```csharp
Arguments = _command,  // <-- Unsafe: no escaping
```

**Attack Scenario**:
- Attacker controls environment variable or factory method that instantiates PathProbe with malicious command string
- Example: `PathProbe("python3; rm -rf /")` → executes secondary command on Unix
- Windows variant: `PathProbe("cmd.exe && del C:\\*.*")`

**Impact**: 
- 🔴 **Severity**: CRITICAL
- Arbitrary command execution in the context of the mlx-pep process
- Requires control over PathProbe instantiation (test injection or configuration)

**Fix**:
```csharp
// Instead of: Arguments = _command
// Use shell escaping or platform-specific safe argument passing:
var arguments = OperatingSystem.IsWindows() 
    ? $"\"{_command}\"" 
    : System.Text.RegularExpressions.Regex.Escape(_command);
Arguments = arguments;
```

---

### 2. MEDIUM: Data Minimization — `RawOutput` Field Over-Exposes Tool Stderr

**Location**: `DependencyDetectionService.cs` → All `Detect*Async()` methods

**Threat**: The service collects and stores `ProbeResult.RawOutput` which contains full stdout/stderr from tool execution:

```csharp
result.RawOutput = probeResult.RawOutput;  // <-- Contains tool stderr
```

**Example Leaks**:
- `pip show model-assessor` stderr might contain pip install paths, Python sys.path entries
- `huggingface-cli --version` stderr could leak HF_HOME path or API endpoint URLs
- Tool errors may include local file paths, Python traceback, internals

**Impact**:
- 🟡 **Severity**: MEDIUM
- Fingerprinting: Attackers see exact tool versions, paths, Python environment
- Information disclosure: Developers' local paths, pip caches, environment details
- Scope: Only visible in JSON output; not shown in CLI table (partial mitigation)

**Design Issue**:
- `RawOutput` is justified for version parsing only
- Storing full output violates data minimization (collect only version strings)

**Fix**:
```csharp
// Instead of storing entire RawOutput, store only version:
result.Version = probe.ParseVersion(probeResult.RawOutput ?? "");
// Don't store: result.RawOutput = probeResult.RawOutput;
// Remove RawOutput property from ToolStatus if not needed for CLI display
```

---

### 3. MEDIUM: Path Traversal — File Path Arguments Unvalidated

**Location**: `FileSystemProbe.cs` → Constructor (lines 8-12)

**Threat**: `FileSystemProbe(_path)` accepts arbitrary file paths with no validation:

```csharp
public FileSystemProbe(string path, bool isDirectory = false)
{
    _path = path;  // <-- No validation; could be "../../../etc/passwd"
    _isDirectory = isDirectory;
}
```

**Scope Detection Usage** (lines 211-223 in DependencyDetectionService):
```csharp
// Detects scope via PATH environment variable — relatively safe
// But if scope detection used VsCodeProbe with user-controlled paths → risk
```

**Attack Scenario**:
- If a future probe accepts user input for `_path` parameter
- Attacker controls tool path: `"../../../../etc/passwd"` → reads system files
- Or: `"/private/var/folders/...` → reads other users' caches

**Impact**:
- 🟡 **Severity**: MEDIUM (not currently exploitable; probes use hardcoded paths)
- **Risk**: Becomes HIGH if path inputs are ever sourced from config files or CLI arguments

**Mitigations Already Present**:
- All paths in FileSystemProbe are hardcoded: `/Applications/oMLX.app`, `/Applications/Visual Studio Code.app`
- PathProbe uses `which`/`where` which is safer than direct path manipulation

**Recommendation**:
```csharp
// Validate path is not attempting traversal:
if (_path.Contains("..") || _path.StartsWith("~"))
    throw new ArgumentException("Path traversal not allowed", nameof(path));
// Alternatively: use Path.GetFullPath() and verify it's within allowed dirs
```

---

### 4. MEDIUM: Injection Vector — Process Arguments via String Concatenation

**Location**: `SystemProcessProbe.cs` → `ProbeAsync()` (line 25)

**Threat**: Process arguments are concatenated without shell escaping:

```csharp
Arguments = _args != null ? string.Join(" ", _args) : "",
```

**Current Usage**:
- `DotnetProbe`: `_args = new[] { "--version" }` → **SAFE** (hardcoded, no user input)
- `Python3Probe`: `_args = new[] { "--version" }` → **SAFE**
- `HuggingFaceCliProbe`: `_args = new[] { "--version" }` → **SAFE**

**Risk Escalation Path**:
- If any probe uses dynamic arguments from environment variables or config: **UNSAFE**
- Example: `new SystemProcessProbe("dotnet", new[] { configValue })` → injection risk

**Impact**:
- 🟡 **Severity**: MEDIUM (currently SAFE due to hardcoded args, but pattern is fragile)
- Future maintainers might add dynamic arguments without escaping

**Fix**:
```csharp
// Use ProcessStartInfo.ArgumentList instead of Arguments string:
process.StartInfo.ArgumentList.Add("--version");  // Automatically escaped
// Or use explicit escaping for shell:
string SafeArg(string arg) => $"\"{arg}\"";  // Basic escaping
```

---

### 5. MEDIUM-LOW: Credential Exposure Risk in Installation Guidance

**Location**: `DependencyInstallationGuidance.cs` → `GetHfCliGuidance()` (line 36)

**Threat**: Installation guidance instructs users to run `huggingface-cli login`:

```csharp
"  Then run: huggingface-cli login"
```

**Risk**:
- If guidance is logged or exposed in CI/CD output, users might paste the command
- HF CLI login writes authentication tokens to `~/.cache/huggingface/` or `~/.config/huggingface/`
- If process environment is captured (e.g., in debugging), `HF_TOKEN` env var could be exposed

**Impact**:
- 🟡 **Severity**: MEDIUM-LOW
- Users are instructed to authenticate interactively (good practice)
- Risk is only if developers inadvertently commit auth tokens or log environment

**Recommendation**:
```csharp
private static string GetHfCliGuidance()
{
    return "Install Hugging Face CLI:\n" +
           "  pip install huggingface-hub\n" +
           "  Then run: huggingface-cli login\n" +
           "  ⚠️  Note: Avoid logging or committing HF_TOKEN environment variable.";
}
```

---

### 6. LOW: Error Handling — Detailed Error Messages Leak Diagnostic Info

**Location**: `DependencyDetectionService.cs` → Exception handling (line 54)

**Threat**: Service-level exception handler logs full exception message:

```csharp
catch (Exception ex)
{
    report.Status = DependencyReportStatus.Failed;
    report.Warnings.Add($"Unexpected error during detection: {ex.Message}");
    // <-- ex.Message might contain file paths, stack traces truncated
}
```

**Example Leaks**:
- `System.ComponentModel.Win32Exception: The system cannot find the path specified` → reveals PATH issues
- `TimeoutException: Operation timed out on localhost:8000` → reveals oMLX HTTP probing
- `UnauthorizedAccessException: Access to the path '/Users/...` → reveals username in file path

**Impact**:
- 🟡 **Severity**: LOW
- Warnings are shown to users via CLI (not hidden)
- Helps with debugging but exposes internal detection strategy

**Fix**:
```csharp
catch (Exception ex)
{
    report.Status = DependencyReportStatus.Failed;
    Debug.WriteLine($"Detection error: {ex}");  // Debug log, not user-facing
    report.Warnings.Add("Dependency detection encountered an error.");
    // Don't expose exception details to users
}
```

---

### 7. Tool-Specific Findings

#### dotnet Probe ✅
- Executes `dotnet --version` → safe, read-only operation
- Version parsing via regex → safe (semantic version pattern only)
- Scope detection via PATH heuristics → safe

#### hf-cli Probe ⚠️ 
- Executes `huggingface-cli --version` → safe
- **Issue**: No model-assessor probe in probes dictionary (line 20)
  - model-assessor is detected via inline `pip show` call in `DetectModelAssessorAsync()` (line 172)
  - **Risk**: `pip show` output might contain paths or API URLs
  - **Recommendation**: Move to probe architecture, parse only version line

#### python3 Probe ✅
- Executes `python3 --version` → safe
- Scope detection → safe

#### model-assessor ✅
- Uses `pip show model-assessor` → safe, read-only
- Regex parsing Version: field → safe

#### oMLX Probe ⚠️
- **File system check**: `/Applications/oMLX.app` → safe (hardcoded path)
- **HTTP health check**: `http://localhost:8000/health` (line 274)
  - **Risk**: If HTTP response contains detailed JSON error, might expose info
  - **Timeout**: 2 seconds → good (prevents hanging)
  - **Recommendation**: Only check status code, ignore body

#### VS Code & VS Code Insiders ✅
- App bundle paths: hardcoded, safe
- PATH probe via `code`/`code-insiders` → safe
- Version detection via `code --version` → safe

#### copilot-cli Probe ⚠️
- Executes `gh copilot --version` → safe
- **Issue**: Runs under user's GitHub authentication
  - If `gh` auth is broken, error might leak auth info
  - **Recommendation**: Wrap `gh` errors with generic message

---

## Compilation Issue Found

**File**: `tests/MlxPep.Core.Tests/Diagnostics/MockProbe.cs`

**Issue**: Missing using directive for `IDependencyProbe` and `ProbeResult`

```csharp
// Missing: using MlxPep.Core.Diagnostics;
public class MockProbe : IDependencyProbe  // <-- CS0246 error
{
    public Task<ProbeResult> ProbeAsync()  // <-- CS0246 error
```

**Fix**: Add at top of file:
```csharp
using MlxPep.Core.Diagnostics;
```

---

## Summary of Recommendations

| Issue | Severity | Category | Fix Effort |
|-------|----------|----------|-----------|
| PathProbe command injection | 🔴 CRITICAL | Injection | 15 min |
| RawOutput over-exposure | 🟡 MEDIUM | Data Minimization | 20 min |
| Path traversal validation | 🟡 MEDIUM | Path Safety | 10 min |
| Process argument escaping | 🟡 MEDIUM | Injection Pattern | 10 min |
| HF login guidance warning | 🟡 MEDIUM-LOW | Credential Hygiene | 5 min |
| Exception message leaking | 🟡 LOW | Error Handling | 10 min |
| MockProbe using directive | 🔴 CRITICAL | Build | 1 min |

---

## Architecture Strengths (RAI+)

✅ **Probe-based design** enables testable, sandboxed execution  
✅ **No shell execution** (`UseShellExecute = false`) prevents shell metacharacter injection  
✅ **Process timeout handling** (5 seconds default) prevents DoS from hanging tools  
✅ **Graceful error handling** ensures service never crashes; always returns structured result  
✅ **Hardcoded paths** in probes prevent user-input path traversal  
✅ **Read-only operations** all tool probes are informational, no side effects  

---

## Traffic Light Verdict

### 🟡 YELLOW → RAI Concerns Detected

**Blockers**: 
1. **Command injection via PathProbe** (CRITICAL)
2. **Compilation error in MockProbe** (CRITICAL — blocks merge)

**Recommendations**:
- Fix PathProbe argument escaping before merge
- Add using directive to MockProbe
- Consider removing RawOutput or restricting to version strings only
- Add warnings to credential-related guidance

**Completion Estimate**: 60–90 minutes for thorough fixes + retesting
