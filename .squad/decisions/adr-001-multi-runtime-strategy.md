# ADR-001: Multi-Runtime Support Strategy for mlx-pep

**Date:** 2026-08-11  
**Author:** Neo (Data/AI/Search Specialist)  
**Status:** Proposed (pending adversarial review)  
**Issue:** #25 — runtimes: mlx-lm / llama.cpp / vLLM support  

---

## Context

mlx-pep currently supports only oMLX as the inference engine for profiling. Issue #25 requests support for additional runtimes:
- **mlx-lm** (Python, Apple Silicon, MLX-based)
- **llama.cpp** (C++, universal platform, GGUF quantization)
- **vLLM** (Python server, enterprise-grade, MLX backend on Apple Silicon)

The project vision is "provide users with well-performing local SLM/LLM models plus the associated system and coding-harness configuration needed to actually run them well on their own hardware." Multi-runtime support expands the ecosystem to serve users with different deployment preferences and constraints.

### Drivers for Multi-Runtime Support
1. **Model Ecosystem Expansion:** mlx-community (500+ models) vs omlx-community (200+ models)
2. **User Choice:** Different runtimes suit different use cases (lightweight cli, production serving, cross-platform)
3. **Cross-Platform Path:** llama.cpp enables future Windows/Linux support (fast-follow)
4. **Integration Flexibility:** Allow profiling on any compatible runtime

### Current State
- Profile schema: `engine` field fixed to `"omlx"`
- CLI: No `--engine` option (implicit oMLX)
- model-assessor integration: Python subprocess (compatible with mlx-lm)

---

## Decision

**Adopt phased multi-runtime strategy with mlx-lm as MVP+1 focus.**

### MVP+1 Scope: mlx-lm (Phase 1)
Extend mlx-pep to support mlx-lm as the first non-oMLX engine:

1. **Schema:** `engine` becomes enum: `"omlx" | "mlx_lm" | "llama_cpp" | "vllm"`
2. **Runtime-specific configs:** Add `mlx_lm` config object; leave llama_cpp/vllm null
3. **CLI:** Add `--engine mlx_lm` option to `assess` command
4. **Profiler:** Implement `MlxLmProfilingRunner` via factory pattern
5. **Detection:** Extend `doctor` to verify mlx-lm installation and version

### Fast-Follow Scope: Conditional Runtimes (Phase 2–3)
- **Phase 2:** llama.cpp (if Windows/Linux in-scope)
- **Phase 3:** vLLM (if production high-throughput serving required)

---

## Rationale

### Why mlx-lm for MVP+1?

| Criterion | mlx-lm | llama.cpp | vLLM |
|-----------|--------|-----------|------|
| **Minimal schema disruption** | ✓ Single config object | ⚠ Requires config + GGUF handling | ⚠ Requires server lifecycle |
| **Shared infrastructure** | ✓ MLX foundation (like oMLX) | ✗ GGML foundation | ✓ MLX backend for Apple Silicon |
| **Python compatibility** | ✓ Works with model-assessor | ⚠ Requires subprocess + llama-cpp-python | ✓ Works with model-assessor |
| **Model ecosystem** | ✓ 500+ mlx-community models | ✓ Unlimited (any HF model via GGUF) | ✓ 500+ mlx-community models |
| **Setup complexity** | ✓ `pip install mlx-lm` | ✗ Binary build or precompiled | ✗ Python 3.12, Sonoma+ only |
| **Apple Silicon acceleration** | ✓ Native Metal (same as oMLX) | ✓ Metal backend (less efficient) | ✓ MLX backend (like mlx-lm) |
| **Operational burden** | ✓ CLI inference | ⚠ GGUF conversion pipeline | ✗ Server process management |
| **Testing scope** | ✓ Straightforward | ⚠ Model format handling | ✗ Concurrency, port conflicts |
| **Integration coupling** | ✓ Minimal (follows oMLX patterns) | ⚠ New binary/conversion dependency | ✗ Server startup/shutdown/health |

**Summary:** mlx-lm requires the fewest integration changes, shares MLX infrastructure with oMLX, avoids new binary dependencies, and provides 2.5x larger model ecosystem than oMLX-only.

### Why Phase Out llama.cpp and vLLM?

- **llama.cpp:** Valuable for cross-platform (Windows/Linux), but adds GGUF conversion complexity. Defer to Phase 2 when cross-platform scope is confirmed.
- **vLLM:** Enterprise-grade serving, but profile workflow is simpler with CLI (mlx-lm) than HTTP server (vLLM). Defer to Phase 3 if high-throughput multi-user profiling becomes a requirement.

### Why Not All Three at Once?

1. **Scope Focus:** MVP+1 should deliver one clear addition. Three runtimes spread implementation, testing, and documentation burden too thin.
2. **De-risking:** mlx-lm is lowest-risk. Success with mlx-lm validates the factory/abstraction pattern; Phase 2/3 can leverage that groundwork.
3. **User Value:** mlx-lm immediately adds 300 new models to the ecosystem. That's the MVP+1 win.

---

## Consequences

### Positive Outcomes
- ✓ Schema designed to be extensible; future runtimes (Phase 2/3) fit within this structure
- ✓ Backward compatible: `mlx-pep assess <model>` defaults to oMLX (no breaking changes)
- ✓ Expands available models: 500+ mlx-community vs 200+ omlx-community
- ✓ Patterns established for multi-engine architecture (factory, interface, detection)
- ✓ Clarifies fast-follow roadmap: teams know when llama.cpp/vLLM will be addressed

### Trade-Offs
- ✗ Profile schema becomes more complex (nullable config objects per engine)
- ✗ CLI users must understand `--engine` option (education needed)
- ✗ mlx-lm model ecosystem limited to community-quantized versions (not all HF models available)
- ✗ Defers Windows/Linux scope to Phase 2 (llama.cpp would enable it sooner)
- ✗ Defers high-throughput serving scope to Phase 3 (vLLM would enable it sooner)

### Mitigations
- Schema validation rules clearly document engine + config pairing (see implementation guide)
- Documentation prominently highlights mlx-community model availability
- Recommend `mlx-pep doctor` upfront to set expectations for supported runtimes
- Decision log (this ADR) makes Phase 2/3 criteria explicit (scope gates, not blockers)

---

## Implementation Strategy

See `docs/implementation-guide-mlx-lm.md` for detailed implementation roadmap. Highlights:

1. **Core Changes** (MlxPep.Core):
   - Extend profile schema with enum `Engine` and nullable config objects
   - Implement abstract `IProfilingRunner` interface
   - Implement `MlxLmProfilingRunner`
   - Add `ProfilingRunnerFactory`

2. **CLI Changes** (MlxPep.Cli):
   - Add `--engine mlx_lm` option to `assess` command
   - Extend `doctor` command to detect mlx-lm

3. **Testing**:
   - Unit tests for MlxLmProfilingRunner
   - Integration tests (requires mlx-lm installed)
   - Schema validation tests

4. **Documentation**:
   - New `docs/engines.md` setup and tuning guide
   - Update `docs/profile-schema.md` with enum and config objects
   - Update `README.md` to list supported runtimes

**Timeline:** 2 weeks (implementation + testing + documentation)

---

## Alternatives Considered

### Alternative 1: All Three Runtimes at Once
**Rejected:** Over-scopes MVP+1; testing and documentation burden increases 3x. Better to validate the pattern with mlx-lm, then add others.

### Alternative 2: Only oMLX (Status Quo)
**Rejected:** Issue #25 explicitly requests multi-runtime support; ecosystem stagnates without it.

### Alternative 3: llama.cpp First Instead of mlx-lm
**Rejected:** llama.cpp requires GGUF conversion pipeline (new dependency). mlx-lm is simpler and fits existing oMLX patterns more closely. llama.cpp's cross-platform value is valuable but scoped to Phase 2 when it can be fully evaluated.

### Alternative 4: Server-First Strategy (vLLM Now)
**Rejected:** Profiling workflow is fundamentally CLI-driven (single-user, fixed test suite). HTTP server adds complexity (process lifecycle, port management) without corresponding benefit for MVP use case. vLLM's value (high-throughput multi-user) is out-of-scope for profiling.

---

## Related Decisions

- **#25 Acceptance Criteria:** "Profile `engine` field supports at least one non-oMLX runtime end-to-end" ✓ Satisfied by mlx-lm MVP+1
- **Cross-Platform Scope:** Windows/Linux support deferred to Phase 2; llama.cpp (universal) will be evaluated then
- **Fast-Follow Roadmap:** Documented in `docs/research/runtimes.md`; Phase 2/3 gates are explicit (scope confirmation required)

---

## Review & Approval

- **Proposed by:** Neo (Data/AI/Search)
- **Date:** 2026-08-11
- **Status:** Pending adversarial review + Morpheus sign-off
- **Expected Outcome:** Architecture approval; kickoff of Phase 1 implementation

**Questions for Reviewers:**
1. Does the phased strategy align with project priorities?
2. Is mlx-lm the right MVP+1 choice, or should we reconsider alternatives?
3. Are the Phase 2/3 scope gates (cross-platform, high-throughput) appropriate?
4. Any integration patterns or dependencies we've overlooked?

---

## Record History

- **2026-08-11:** Initial proposal + research findings delivered
