# mlx-lm Integration Checklist

**Epic:** #25 — runtimes: mlx-lm / llama.cpp / vLLM support  
**Phase:** MVP+1 (mlx-lm focus)  
**Status:** Implementation Planning  

---

## Pre-Implementation Review

- [ ] Adversarial review complete + feedback addressed
- [ ] Morpheus approves architecture (ADR-001)
- [ ] Team consensus on mlx-lm as MVP+1 choice
- [ ] Phase 2/3 scope gates accepted (cross-platform, high-throughput)

---

## Phase 1: Core Implementation

### Schema & Data Model
- [ ] Update `Profile.cs`: Add `Engine` enum
- [ ] Add `MlxLmConfig` class
- [ ] Update `Profile` class: add nullable engine configs
- [ ] Add schema validation: exactly one engine config must be non-null
- [ ] Update `docs/profile-schema.md` with enum and new fields
- [ ] JSON serialization test: roundtrip Profile to/from JSON

### Profiler Interface & Factory
- [ ] Create `IProfilingRunner` interface
  - [ ] `VerifyInstalledAsync(): Task<bool>`
  - [ ] `ProfileAsync(...): Task<ProfilingResult>`
  - [ ] `GetVersionAsync(): Task<string?>`
- [ ] Create `MlxLmProfilingRunner` implementation
  - [ ] `VerifyInstalledAsync()`: Check `python3 -m mlx_lm.generate --help`
  - [ ] `ProfileAsync()`: Run profiling test suite, parse metrics
  - [ ] `GetVersionAsync()`: Extract version from `mlx_lm --version` or similar
- [ ] Create `ProfilingRunnerFactory`
  - [ ] Route `Engine.OMLx` → existing oMLX runner
  - [ ] Route `Engine.MlxLm` → `MlxLmProfilingRunner`
  - [ ] Route `Engine.LlamaCpp`, `Engine.VLLm` → throw `NotSupportedException` with helpful message

### CLI: assess Command
- [ ] Add `--engine` option (default: `"omlx"`)
- [ ] Parse engine string to `Engine` enum
- [ ] Use factory to create runner
- [ ] Call `VerifyInstalledAsync()` before profiling
- [ ] Provide install guidance if runtime missing
- [ ] Build profile with correct engine + config
- [ ] Serialize profile JSON with `engine="mlx_lm"` field

### CLI: doctor Command
- [ ] Add runtime detection loop
- [ ] For each `Engine` enum value:
  - [ ] Call `VerifyInstalledAsync()`
  - [ ] If found, call `GetVersionAsync()`
  - [ ] Report status: `✓ mlx-lm 0.19.2` or `✗ mlx-lm — install: pip install mlx-lm`
- [ ] Link to `docs/engines.md` for install instructions

---

## Phase 2: Testing

### Unit Tests
- [ ] `MlxLmProfilingRunnerTests.cs`
  - [ ] `VerifyInstalledAsync_WithMlxLmInstalled_ReturnsTrue`
  - [ ] `VerifyInstalledAsync_WithoutMlxLm_ReturnsFalse`
  - [ ] `ProfileAsync_WithValidModel_ReturnMetrics` (mock subprocess)
  - [ ] `ProfileAsync_WithMissingModel_ThrowsHumanReadableError`
  - [ ] `ProfileAsync_WithTimeout_ThrowsTimeoutException`
  - [ ] `GetVersionAsync_WithMlxLmInstalled_ReturnsVersion`

### Integration Tests
- [ ] `AssessCommandIntegrationTests.cs` (requires mlx-lm installed)
  - [ ] `AssessCommand_WithMlxLmEngine_ProducesValidProfile`
  - [ ] `AssessCommand_WithMlxLmModel_ProfileJsonValidatesAgainstSchema`
  - [ ] `AssessCommand_WithMissingModel_ReturnsHelpfulError`
  - [ ] Test against small mlx-community model (e.g., Mistral-7B-4bit)
  - [ ] Verify profile output can be applied with `mlx-pep apply`

### Schema Validation Tests
- [ ] `ProfileSchemaTests.cs`
  - [ ] `Profile_WithMlxLmEngine_OnlyMlxLmConfigNonNull`
  - [ ] `Profile_WithOMLxEngine_OnlyOMLxConfigNonNull`
  - [ ] `Profile_WithMultipleEngineConfigs_FailsValidation`
  - [ ] `Profile_WithNoEngineConfig_FailsValidation`
  - [ ] `Profile_JsonRoundtrip_PreservesAllFields`

### Doctor Tests
- [ ] `DoctorCommandTests.cs`
  - [ ] `DoctorCommand_WithMlxLmInstalled_ReportsVersion`
  - [ ] `DoctorCommand_WithoutMlxLm_ReportsNotFound`
  - [ ] Output includes install guidance for missing runtimes

---

## Phase 3: Documentation

### docs/engines.md (New Comprehensive Guide)
- [ ] **mlx-lm Setup**
  - [ ] Installation: `pip install mlx-lm`
  - [ ] Verification: `python3 -m mlx_lm.generate --help`
  - [ ] Model availability: link to mlx-community on HF
  - [ ] Quick start: `mlx-pep assess <model-id> --engine mlx_lm`
- [ ] **Profiling Workflow**
  - [ ] Step-by-step: download model → run assess → view profile
  - [ ] Expected output: tokens/sec, memory, latency
  - [ ] Profile schema fields explained (quantization, max_tokens, use_cache)
- [ ] **Tuning Parameters**
  - [ ] Quantization: 4bit vs 8bit vs fp16 (trade-offs)
  - [ ] Context window (2K, 4K, 8K)
  - [ ] Batch size
  - [ ] Temperature, top-p, top-k (sampler defaults)
- [ ] **Hardware-Specific Hints**
  - [ ] M1 (8-core): recommmend 4-bit quantization for 30B+ models
  - [ ] M3 (12-core): can handle 70B with 4-bit
  - [ ] M4 Max (12-core + 40-core GPU): optimal for large context
  - [ ] Reference benchmark data (if available)
- [ ] **Troubleshooting**
  - [ ] "Model not found": check mlx-community availability, link to Hub
  - [ ] "Out of memory": try lower quantization or smaller model
  - [ ] "Slow generation": check system load, disable other apps
  - [ ] "Version mismatch": pin mlx-lm in requirements
- [ ] **Phase 2/3 Preview**
  - [ ] Note on llama.cpp (cross-platform, GGUF quantization)
  - [ ] Note on vLLM (high-throughput serving)
  - [ ] When to consider each based on use case

### Update docs/profile-schema.md
- [ ] Document `engine` enum: `"omlx" | "mlx_lm" | "llama_cpp" | "vllm"`
- [ ] Add `MlxLmConfig` schema:
  ```
  "mlx_lm": {
    "quantization": "4bit" | "8bit" | "fp16",
    "maxTokens": number,
    "useCache": boolean
  }
  ```
- [ ] Show example profile for mlx-lm alongside oMLX example
- [ ] Explain nullable config pattern: exactly one engine config must be non-null

### Update README.md
- [ ] Add to features or new "Supported Runtimes" section:
  ```
  - **Inference Engines:** oMLX, mlx-lm (MVP+1), 
    with fast-follow support for llama.cpp and vLLM
  - See [docs/engines.md](docs/engines.md) for runtime selection guide
  ```

### Docstrings & API Docs
- [ ] Docstring on `Engine` enum: explain each value
- [ ] Docstring on `IProfilingRunner`: explain contract
- [ ] Docstring on `MlxLmProfilingRunner`: explain mlx-lm-specific behavior
- [ ] Docstring on `ProfilingRunnerFactory`: explain runtime selection
- [ ] Docstring on `--engine` CLI option: explain choices, defaults, install guidance

---

## Phase 4: Code Review & Merge

### Pre-Merge Checklist
- [ ] All unit tests pass locally
- [ ] All integration tests pass (mlx-lm installed)
- [ ] Code review + approval from Morpheus
- [ ] No breaking changes to existing oMLX profiling
- [ ] Documentation complete and reviewed
- [ ] Backward compatibility verified: `mlx-pep assess <model>` still works (defaults to oMLX)

### CI/CD Pipeline
- [ ] Unit tests run on every commit
- [ ] Integration tests run on PR (require mlx-lm in CI environment)
- [ ] Linting passes (code style, null checks, etc.)
- [ ] Documentation builds without warnings

### Merge & Tag
- [ ] Merge to main
- [ ] Tag release: `v{version}-mlx-lm` or similar
- [ ] Update CHANGELOG.md
- [ ] Announce in team notes

---

## Phase 5: Post-Launch

### Monitoring
- [ ] Track `mlx-pep assess --engine mlx_lm` usage (if telemetry available)
- [ ] Collect user feedback: which mlx-community models work well, pain points
- [ ] Monitor error reports: model availability, version conflicts, timeout issues

### Fast-Follow Prep
- [ ] Evaluate Phase 2/3 scope gates based on user feedback
- [ ] Start research on llama.cpp integration (if cross-platform requested)
- [ ] Start research on vLLM integration (if high-throughput requested)

---

## Success Metrics

- ✅ `mlx-pep assess <mlx-community-model> --engine mlx_lm` produces valid JSONL
- ✅ Profile schema validation passes (engine + config pairing correct)
- ✅ `mlx-pep doctor` detects mlx-lm and reports version
- ✅ Integration test passes end-to-end on Apple Silicon hardware
- ✅ Backward compatibility: existing oMLX profiling workflow unchanged
- ✅ Documentation is complete, clear, and discoverable
- ✅ Zero bugs in MVP+1 release (or handled in fast-follow patch)

---

## Open Questions (Addressed Before Phase 1 Kickoff)

1. **Model versioning:** Should mlx-lm version be captured in profile metadata?
   - *Proposal:* Yes, add to `provenance` or new `runtime` metadata field
2. **Quantization auto-detect:** Can mlx-lm auto-detect the quantization level used?
   - *Proposal:* Auto-detect from model config if possible; else require user to specify
3. **Timeout handling:** What timeout (seconds) for profiling large models (70B+)?
   - *Proposal:* 300 seconds (5 min) default; make configurable
4. **Parallel profiling:** Should `--engine mlx_lm` and `--engine omlx` run in parallel?
   - *Proposal:* Sequential for now (simpler); parallel as future optimization if needed

---

## Related Issues & PRs

- Issue #25: runtimes: mlx-lm / llama.cpp / vLLM support
- ADR-001: Multi-runtime support strategy
- docs/research/runtimes.md: Comprehensive runtime analysis
- docs/implementation-guide-mlx-lm.md: Detailed implementation guide

---

**Last Updated:** 2026-08-11 (Pre-implementation)  
**Next Update:** After adversarial review completes
