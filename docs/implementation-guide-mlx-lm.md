# Implementation Guide: mlx-lm Runtime Support (MVP+1)

**Status:** Planning (pending adversarial review)  
**Assigned:** Squad (Morpheus + Neo)  
**Epic:** #25 — runtimes: mlx-lm / llama.cpp / vLLM support  

---

## Overview

This guide outlines the MVP+1 implementation strategy for adding mlx-lm as the first non-oMLX profiling engine to mlx-pep. The work is scoped to:
- Extend profile schema to support multiple engines
- Implement mlx-lm profiler variant
- Update CLI and dependency detection (`doctor`)
- Add integration tests and documentation

---

## 1. Schema Changes

### 1.1 Profile Schema Evolution

**File:** `docs/profile-schema.md`

Current state:
```json
"engine": "omlx"
```

Target state:
```json
"engine": "mlx_lm" | "omlx" | "llama_cpp" | "vllm",
"omlx": { /* existing */ },
"mlx_lm": { /* new */ }
```

### 1.2 .NET Model Changes

**File:** `MlxPep.Core/Profile.cs` (or equivalent)

Add:
```csharp
public enum Engine
{
    OMLx,
    MlxLm,
    LlamaCpp,
    VLLm
}

public class MlxLmConfig
{
    public string Quantization { get; set; } // "4bit", "8bit", "fp16"
    public int MaxTokens { get; set; }
    public bool UseCache { get; set; }
}

public class Profile
{
    public Engine Engine { get; set; }
    public OMLxConfig? OMLxConfig { get; set; }
    public MlxLmConfig? MlxLmConfig { get; set; }
    // llama_cpp and vllm configs as needed
}
```

### 1.3 Validation Rules

- Exactly one engine-specific config (omlx, mlx_lm, etc.) must be non-null
- `engine` field must match the non-null config object
- System/harness/sampler/provenance fields remain required for all engines

---

## 2. Core Implementation

### 2.1 Abstract Profiling Interface

**File:** `MlxPep.Core/Profiling/IProfilingRunner.cs`

```csharp
public interface IProfilingRunner
{
    /// <summary>
    /// Verify the runtime is installed and accessible.
    /// Throws if missing or incompatible.
    /// </summary>
    Task<bool> VerifyInstalledAsync();

    /// <summary>
    /// Run profiling test suite and return metrics.
    /// </summary>
    Task<ProfilingResult> ProfileAsync(
        string modelHfId,
        string modelPath,
        ProfilingOptions options,
        CancellationToken ct = default);

    /// <summary>
    /// Get runtime version string for doctor output.
    /// </summary>
    Task<string?> GetVersionAsync();
}

public class ProfilingResult
{
    public double TokensPerSecond { get; set; }
    public double PeakMemoryMb { get; set; }
    public double AverageLatencyMs { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}
```

### 2.2 mlx-lm Profiler Implementation

**File:** `MlxPep.Core/Profiling/MlxLmProfilingRunner.cs`

Core responsibilities:
1. Verify mlx-lm installed: `python3 -m mlx_lm.generate --help`
2. Check model availability in mlx-community ecosystem
3. Run profiling subprocess with fixed test suite
4. Parse output and collect metrics
5. Emit error handling for missing models or incompatible versions

```csharp
public class MlxLmProfilingRunner : IProfilingRunner
{
    public async Task<bool> VerifyInstalledAsync()
    {
        var proc = new ProcessStartInfo
        {
            FileName = "python3",
            Arguments = "-m mlx_lm.generate --help",
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        // Execute and check return code
    }

    public async Task<ProfilingResult> ProfileAsync(...)
    {
        // 1. Resolve model path from HF cache or mlx-community
        // 2. Build command: mlx_lm.generate --model <path> --prompt <test> ...
        // 3. Run with timeout, collect metrics
        // 4. Parse output: tokens/sec, memory, latency
        // 5. Return ProfilingResult
    }
}
```

### 2.3 Profiling Runner Factory

**File:** `MlxPep.Core/Profiling/ProfilingRunnerFactory.cs`

```csharp
public class ProfilingRunnerFactory
{
    public static IProfilingRunner CreateRunner(Engine engine)
    {
        return engine switch
        {
            Engine.OMLx => new OMLxProfilingRunner(...),
            Engine.MlxLm => new MlxLmProfilingRunner(...),
            Engine.LlamaCpp => throw new NotSupportedException("llama.cpp: Phase 2"),
            Engine.VLLm => throw new NotSupportedException("vLLM: Phase 3"),
            _ => throw new ArgumentException($"Unknown engine: {engine}")
        };
    }
}
```

---

## 3. CLI Changes

### 3.1 assess Command

**File:** `MlxPep.Cli/Commands/AssessCommand.cs`

Add option:
```bash
mlx-pep assess <model-id> --engine [omlx|mlx_lm|llama_cpp|vllm]
```

Default: `omlx` (backward compatible)

Implementation:
```csharp
[Option("--engine", Description = "Runtime engine: omlx (default), mlx_lm, llama_cpp, vllm")]
public string Engine { get; set; } = "omlx";

public async Task<int> ExecuteAsync()
{
    var engine = Enum.Parse<Engine>(Engine, ignoreCase: true);
    var runner = ProfilingRunnerFactory.CreateRunner(engine);
    
    // Verify installed
    if (!await runner.VerifyInstalledAsync())
        return Cli.Error($"Runtime {engine} not found. Install with: ...");
    
    // Run profiling
    var result = await runner.ProfileAsync(...);
    
    // Generate profile JSON with engine field
    var profile = BuildProfile(result, engine);
    Console.WriteLine(JsonSerializer.Serialize(profile));
}
```

---

## 4. Dependency Detection

### 4.1 doctor Command Extension

**File:** `MlxPep.Cli/Commands/DoctorCommand.cs`

Add runtime detection:
```
Runtime Support:
  ✓ oMLX 0.2.5 detected at /opt/omlx/bin/omlx
  ✓ mlx-lm 0.19.2 installed (python3 -m mlx_lm.generate)
  ✗ llama.cpp not found — install: brew install llama-cpp
  ✗ vLLM not found — install: pip install vllm-metal
```

Implementation:
```csharp
private async Task DetectRuntimesAsync()
{
    foreach (var engine in Enum.GetValues<Engine>())
    {
        var runner = ProfilingRunnerFactory.CreateRunner(engine);
        var installed = await runner.VerifyInstalledAsync();
        var version = installed ? await runner.GetVersionAsync() : null;
        
        if (installed)
            Console.WriteLine($"✓ {engine} {version} detected");
        else
            Console.WriteLine($"✗ {engine} not found — install: {GetInstallCommand(engine)}");
    }
}
```

---

## 5. Testing Strategy

### 5.1 Unit Tests

**File:** `MlxPep.Core.Tests/Profiling/MlxLmProfilingRunnerTests.cs`

Test cases:
- `VerifyInstalledAsync_WithMlxLmInstalled_ReturnsTrue`
- `VerifyInstalledAsync_WithoutMlxLm_ReturnsFalse`
- `ProfileAsync_WithValidModel_ReturnMetrics`
- `ProfileAsync_WithMissingModel_ThrowsHumanReadableError`
- `ProfileAsync_WithTimeout_ThrowsTimeoutException`

### 5.2 Integration Tests

**File:** `MlxPep.Cli.Tests/Commands/AssessCommandIntegrationTests.cs`

Test cases (requires mlx-lm installed in test environment):
- `AssessCommand_WithMlxLmEngine_ProducesValidProfile`
- `AssessCommand_WithMlxLmModel_ProfileJsonValidatesAgainstSchema`
- `AssessCommand_WithMissingModel_ReturnsHelpfulError`

### 5.3 Profile Schema Validation

**File:** `MlxPep.Core.Tests/ProfileSchemaTests.cs`

Test cases:
- `Profile_WithMlxLmEngine_OnlyMlxLmConfigNonNull`
- `Profile_WithOMLxEngine_OnlyOMLxConfigNonNull`
- `Profile_WithMultipleEngineConfigs_FailsValidation`
- `Profile_WithNoEngineConfig_FailsValidation`

---

## 6. Documentation Updates

### 6.1 docs/engines.md (New)

Create comprehensive guide covering:
- **mlx-lm Setup**
  - `pip install mlx-lm`
  - Verify: `python3 -m mlx_lm.generate --help`
  - Model availability (mlx-community on HF)
- **Profiling with mlx-lm**
  - `mlx-pep assess <model> --engine mlx_lm`
  - Expected output format and metrics
- **Tuning Parameters**
  - Quantization: 4bit vs 8bit vs fp16
  - Context window, batch size
  - Hardware-specific hints (M1 vs M3 vs M4)
- **Troubleshooting**
  - Model not found → check mlx-community availability
  - Out of memory → try lower quantization
  - Performance degradation → check system load

### 6.2 Update docs/profile-schema.md

- Document `engine` enum values
- Show example profiles for mlx-lm (alongside omlx)
- Explain `mlx_lm` config object fields

### 6.3 Update README.md

Add to feature list:
- "Support for multiple inference engines (oMLX, mlx-lm; llama.cpp and vLLM in fast-follow)"

---

## 7. Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| **mlx-lm model availability** | Maintain curated list of tested models; document mlx-community limitations |
| **Version drift** | Pin mlx-lm version in requirements; add version check in doctor |
| **Memory pressure on large models** | Test quantization levels; document per-hardware constraints |
| **Python environment conflicts** | Recommend virtual environment; validate Python 3.8+ in doctor |

---

## 8. Rollout Plan

### Phase 0: Review & Approval (Current)
- Await adversarial review findings
- Address any issues or scope adjustments
- Finalize decision

### Phase 1: Implementation (Week 1)
- [ ] Implement schema changes + validation (Core)
- [ ] Implement MlxLmProfilingRunner (Core)
- [ ] Implement CLI `--engine` option (Cli)
- [ ] Add doctor runtime detection (Cli)

### Phase 2: Testing (Week 1–2)
- [ ] Unit tests for profiler
- [ ] Integration tests (requires mlx-lm environment)
- [ ] Schema validation tests
- [ ] Manual smoke tests on representative hardware

### Phase 3: Documentation (Week 2)
- [ ] Create docs/engines.md
- [ ] Update profile-schema.md
- [ ] Update README.md
- [ ] Docstrings for new public APIs

### Phase 4: Merge & Release (Week 2)
- [ ] Code review + sign-off from Morpheus
- [ ] Merge to main
- [ ] Tag release (MVP+1)

---

## 9. Success Criteria

- [ ] `mlx-pep assess <mlx-community-model> --engine mlx_lm` produces valid JSONL
- [ ] Profile validates against schema with `engine="mlx_lm"`
- [ ] `mlx-pep doctor` detects mlx-lm and reports version
- [ ] Integration test passes on Apple Silicon hardware
- [ ] Comprehensive documentation in docs/engines.md
- [ ] Zero breaking changes to existing oMLX profiling workflow

---

## 10. Open Questions

1. **Model versioning:** Should mlx-lm version be captured in profile metadata?
2. **Quantization detection:** Can mlx-lm auto-detect the quantization level used, or must it be user-specified?
3. **Timeout handling:** What timeout (seconds) is reasonable for profiling a large model (70B+)?
4. **Parallel profiling:** Should `--engine mlx_lm` and `--engine omlx` be runnable in parallel (separate processes), or sequential?

---

**Next Steps:** Address adversarial review findings, finalize scope, and kick off Phase 1 implementation.
