# PR #64 Adversarial Review — `mlx-pep doctor`

**Verdict:** Conditional approval after fixes.

## Blocking issues

### 1. JSON mode emits two top-level JSON documents
**Why this blocks:** `DoctorCommand.ExecuteAsync()` already writes the full JSON payload, but `CliBuilder.HandleDoctor()` appends a second `{ "message", "exit_code" }` JSON object. That makes `mlx-pep doctor --json` invalid for every machine consumer expecting one document, and it directly breaks the CLI contract for `--json` output.

**Where:**
- `src/MlxPep.Cli/Commands/DoctorCommand.cs:27-30, 44-59`
- `src/MlxPep.Cli/CliBuilder.cs:71-86`

**Observed behavior on this machine:**
```json
{
  "command": "doctor",
  "timestamp": "...",
  "dependencies": { ... }
}
{"message":null,"exit_code":0}
```

**How to fix:** Make doctor follow the same command contract as the richer commands in the repo: either return structured data through `CommandResult.Data` and let `CliBuilder` serialize once, or let `DoctorCommand` own all output and make `CliBuilder.HandleDoctor()` return the exit code without printing anything else.

**Concrete direction:**
```csharp
private static async Task<int> HandleDoctor(bool isJson)
{
    var handler = new DoctorCommand();
    var result = await handler.ExecuteAsync(new CommandContext(isJson));
    return result.ExitCode;
}
```
Or better, refactor `DoctorCommand` to return a payload object and centralize JSON formatting in `CliBuilder`.

### 2. The command is not wired to the dependency detection service from #11
**Why this blocks:** Issue #13 explicitly says this command should report the states detected by the dependency detection work from #11. The current implementation hardcodes seven probes inside `DoctorCommand` and bypasses any reusable dependency/service abstraction. That creates drift risk between the doctor command and the underlying platform detection rules, especially for MLX/oMLX naming, installer guidance, and future detector expansion.

**Where:**
- `src/MlxPep.Cli/Commands/DoctorCommand.cs:16-24, 97-231`

**Why I believe this is a real gap:**
- The PR introduces bespoke `ProcessStartInfo` checks directly in the CLI command.
- I could not find any shared detector/service usage in this command path.
- The issue asks for states to be reported correctly *on this machine* using dependency detection work that already exists or is expected from #11.

**How to fix:** Move detection ownership out of `DoctorCommand` and consume the shared dependency detection service/result model. `DoctorCommand` should be a presentation layer only:
1. call the detector service,
2. map result records to table/JSON output,
3. preserve install guidance from the detector metadata.

**Concrete direction:**
```csharp
var detector = new DependencyDetectionService(...);
var report = await detector.DetectAsync(cancellationToken);
OutputJson(report);
```
If #11 exposes a different API, adapt this shape to that existing contract instead of keeping separate process logic here.

### 3. Install guidance is missing from JSON mode and incomplete in table mode
**Why this blocks:** The issue task says: "table of dependencies with status + install guidance; `--json` variant." The current table mode only tells the user to rerun with `--json`, and the current JSON payload contains only `installed`, `version`, and `message`. There is no install command, docs URL, remediation hint, or platform-specific guidance in either path.

**Where:**
- `src/MlxPep.Cli/Commands/DoctorCommand.cs:62-94, 254-265`

**How to fix:** Include an install/remediation field in the dependency result model and display it in both modes. Example JSON shape:
```json
"hf-cli": {
  "installed": false,
  "message": "huggingface-cli not found in PATH",
  "install": "python3 -m pip install huggingface_hub[cli]"
}
```
For table mode, print a short remediation line under each missing dependency or add a compact "Suggested fix" column when wrapping is acceptable.

## Medium recommendations

### 1. Add timeouts/cancellation for external process probes
`WaitForExitAsync()` without a timeout can hang indefinitely on a broken shell shim, PATH wrapper, or misbehaving executable. For a doctor command this is a real UX/perf problem because one bad dependency can freeze the whole report. Bound each probe with a timeout and return a deterministic timeout status.

### 2. Probe `python3 -m pip` instead of bare `pip`
The current oMLX detection assumes `pip` is on PATH and maps to the same interpreter as `python3`. On macOS and mixed Python installs that is often false. If the contract is "python + model-assessor state," use `python3 -m pip show mlx-lm` so the detector follows the python executable you already reported.

### 3. Harden version parsing
`ExtractVersion()` currently splits on `v`/`V`, which truncates the Copilot CLI version on this machine to `1.0.79.` instead of the full value. Prefer command-specific parsing or a regex that preserves semver/prerelease/build suffixes.

### 4. Remove or replace the placeholder `UnitTest1`
The PR adds useful doctor tests, but `tests/MlxPep.Cli.Tests/UnitTest1.cs` is still present as an empty placeholder and inflates the passing test count. Delete it or replace it with a real smoke test.

### 5. Strengthen tests around the real contract
The added tests validate `DoctorCommand` in isolation, but the production break is in the full CLI path (`CliBuilder`). Add an integration test that invokes the CLI doctor route with `--json` and asserts a single valid JSON document.

## Strengths

- Table output is substantially more readable than the stubbed implementation and uses friendly names.
- The dependency list matches the issue acceptance set closely enough to be directionally right.
- JSON naming was improved to lowercase field names and omits nulls, which is a good external contract direction.
- The command handles missing tools without shelling out through a shell interpreter, which avoids injection risk.

## Security assessment

**Current:** 6/10  
**After fixes:** 8/10

Rationale: The implementation avoids shell execution and does not expose secrets, but it swallows exceptions silently in several probes, has no timeout boundaries, and currently returns ambiguous/incomplete remediation data. Fixing the output contract, wiring to the shared detector, and adding bounded process execution would materially improve trustworthiness.

## Next steps for the author

1. Fix the double-JSON contract in the CLI path first.
2. Replace the command-local process probing with the shared dependency detection service from #11.
3. Extend the result model with installation guidance/remediation and expose it in both JSON and human output.
4. Add one end-to-end CLI JSON test and remove the placeholder test file.
