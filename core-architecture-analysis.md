# Core Architecture Analysis — Issues #8–#11

Date: 2026-08-12

## Executive summary

The profiling pipeline has a single foundational dependency: profile schema stability. Once the JSON contract and validation rules for a profile set are locked, the rest of the system can be implemented as parallel runtime probes that feed a common model.

Recommended dependency order:

1. #8: Profile schema records + STJ source-gen + JSONL validation
2. #9: Shared Hugging Face cache reader
3. #10: System + oMLX read-only detectors
4. #11: Dependency detection service

This sequencing preserves a clean dependency graph:
- #8 establishes the canonical data contract.
- #9 produces local model inventory and cache evidence.
- #10 supplies environment/runtime facts.
- #11 consumes profile + environment data to infer model dependency relationships.

## Dependency graph

```text
#8 Profile schema
   ├── enables #9 cache reader (model inventory normalization)
   ├── enables #10 detectors (runtime/system facts in the same schema shape)
   └── enables #11 dependency detection (schema contract for inputs/outputs)

#9 HF cache reader
   └── feeds downstream matching/analysis, not a blocker for #10/#11 design

#10 System + oMLX detectors
   └── independent runtime data source for #11

#11 Dependency detection service
   └── depends on #8 + #9 + #10 data contracts and normalized outputs
```

Critical path:
- The real critical path is #8 first, then #11 after the runtime probes are in place.
- #9 and #10 are parallelizable and primarily additive.
- #11 is the integration layer where all upstream facts are combined.

## Recommended implementation order

### #1: Issue #8 — Profile schema foundation
Why first:
- Defines the canonical data model for all later services.
- Locks JSONL round-tripping, validation rules, and machine-readable schema.
- Gives CLI, service, and cache/detector work a shared contract.

### #2: Issue #9 — Shared Hugging Face cache reader
Why second:
- Independent of runtime detection, but needs the same profile/input conventions to normalize local model metadata.
- Provides inventory and provenance for model discovery before dependency analysis.

### #3: Issue #10 — System + oMLX detectors
Why third:
- Produces the environment facts the dependency analyzer needs.
- Cross-platform detection is a separate concern and should be isolated behind a small interface.

### #4: Issue #11 — Dependency detection service
Why last:
- Requires all upstream sources to be stable and normalized.
- Highest complexity and highest integration risk.

## Per-issue architecture notes

### Issue #8: Profile schema records + STJ source-gen + JSONL validation

Data model sketch:
```csharp
public record Profile(
    int SchemaVersion,
    string Id,
    string ModelHfId,
    string Tier,
    string Engine,
    Dictionary<string, object> System,
    Dictionary<string, object> OMLXSettings,
    Dictionary<string, object> Harness,
    ProfileProvenance Provenance,
    HardwareFingerprint Hardware,
    SamplerSettings? Sampler,
    CommunityMetadata? Community);
```

Key structures:
- Profile: canonical profile record for a single model/runtime profile.
- Tier: canonical values should normalize to a small set; compatibility aliases can be accepted for migration.
- System / oMLX / Harness: dictionaries to allow forward compatibility and runtime-specific extension.
- Provenance: source, author, timestamp, traceability.
- HardwareFingerprint: device identity and memory characteristics.
- CommunityMetadata: optional publish-time metadata.

Key decisions:
- Prefer a stable record-based model over a dynamic schema tree.
- Keep system-specific fields in dictionaries so the core contract is not over-constrained.
- Use System.Text.Json source generation to avoid reflective overhead and make serialization deterministic.
- Treat unknown keys as warnings rather than hard failures to preserve forward compatibility.

Validation rules:
- A profile set must have unique tier values per set, or be explicitly treated as a multi-tier bundle.
- Unknown keys in system/omlx/harness are warning-only.
- JSONL format: one object per line, no envelope, no trailing separators.

Effort estimate:
- 2–4 days for design + implementation + tests.

Risk:
- Source generation and custom converters can recurse or over-constrain the JSON contract.
- Compatibility drift between canonical tier names and legacy project data.

Readiness:
- Ready to code now.

### Issue #9: Shared Hugging Face cache reader

Data model sketch:
```csharp
public record CacheEntry(
    string ModelId,
    string CachePath,
    string? Snapshot,
    long? SizeBytes,
    DateTimeOffset LastSeen,
    Dictionary<string, object> Metadata);
```

Key decisions:
- Abstract cache discovery behind a single provider interface.
- Normalize both local disk-based cache and metadata-based sources to a common record model.
- Cache reads should be read-only and side-effect free.
- Add a lightweight in-memory cache or index so repeated lookups do not re-scan the filesystem.

Feasibility:
- High. The design is straightforward if the project already has a well-defined local cache layout.
- Needs validation of the real HF cache directory layout across supported OSes.

Performance strategy:
- Read directory metadata once and memoize.
- Avoid walking the full repo tree repeatedly during CLI or service runs.
- Prefer fast path: directory listing + model-name normalization before deeper metadata reads.

Effort estimate:
- 2–3 days for implementation + test coverage.

Risk:
- Cache layout differences between user installs and managed installs.
- Stale metadata and incomplete model manifests.

Readiness:
- Ready to code with a short research pass on actual cache paths.

### Issue #10: System + oMLX read-only detectors

Data model sketch:
```csharp
public interface IRuntimeDetector
{
    string Name { get; }
    IReadOnlyDictionary<string, object> Detect();
}
```

Key decisions:
- Keep detection logic read-only and side-effect free.
- Separate platform detection from status interpretation.
- Model detector output as a dictionary so new hardware/runtime metrics can be added without changing the public contract.

Cross-platform concerns:
- macOS: Apple silicon vs Intel, memory, storage, metal capabilities.
- Linux: GPU availability, driver state, CPU topology, thermal constraints.
- Windows: WSL, GPU/driver layout, local file system semantics.
- Need graceful degradation when a platform-specific capability is unavailable.

oMLX notes:
- Detect whether the environment is compatible with oMLX execution and whether the runtime is actually available.
- Keep capability checks as fast, no-op-safe probes.

Effort estimate:
- 3–5 days, depending on cross-platform coverage and test harness complexity.

Risk:
- Different environments produce inconsistent “runtime available” signals.
- Edge-case detection for hybrid systems (WSL, ARM, VM, containerized installs).

Readiness:
- Mostly ready, but the platform matrix should be confirmed before locking the detector contract.

### Issue #11: Dependency detection service

Data model sketch:
```csharp
public record DependencyEvidence(
    string ModelId,
    string DependencyName,
    string Kind,
    double Confidence,
    IReadOnlyDictionary<string, object> Evidence);
```

Key decisions:
- Dependency detection should operate as an analysis service, not a parser-only utility.
- Feed it normalized model metadata + system runtime facts + cache inventory.
- Keep the algorithm deterministic and explainable; confidence scoring should be traceable to evidence.

Technical approach:
- AST or metadata parsing for model dependency declarations when available.
- ML model integration only when using model-level hints or manifest metadata.
- Treat dependency discovery as evidence aggregation and ranking rather than binary inference.

Integration requirements:
- Input: profile(s), cache inventory, runtime facts, optional model metadata.
- Output: structured dependency list with confidence and evidence.

Effort estimate:
- 4–6 days, high risk and integration-heavy.

Risk:
- Overfitting to one model or runtime.
- Ambiguous dependency classification when evidence is sparse.
- Need for model-specific adapters.

Readiness:
- Not ready for full implementation before #8–#10 are stable.

## Risk assessment

High-risk items:
- Issue #8: schema drift and JSON source-generation recursion/custom converter issues.
- Issue #11: ambiguous dependency semantics and uncertain evidence quality.

Medium-risk items:
- Issue #9: differing HF cache layouts across users/system setups.
- Issue #10: cross-platform runtime detection differences and false positives.

Low-risk items:
- Standardized record models and serializer setup for issue #8 once the contract is fixed.

## Implementation readiness

Overall status:
- #8: Ready to code now.
- #9: Ready to code with a focused environment validation pass.
- #10: Ready to code with platform matrix research before finalizing edge-case rules.
- #11: Not ready for final implementation until #8–#10 are baseline-complete.

## Recommended dispatch summary for Ralph

Issue order:
- #8 first
- #9 second
- #10 third
- #11 last

Ownership:
- Neo / Core: #8, #11
- Trinity: CLI-facing integration and profile UX after schema is stabilized
- Tank: middleware/runtime plumbing and cross-platform detector support
- Consumer teams: CLI and service integration after contract stabilization

Phase placement:
- Phase 2B is appropriate for #8 + #9.
- Phase 3 is the correct window for #10 + #11 once the evidence pipeline is stable.

## Final recommendation

Treat #8 as the hard gate. Once the schema and validation semantics are stable, issue #9 can proceed in parallel with early design work for #10, while #11 remains the final integration milestone. This reduces architectural churn and keeps the downstream tooling aligned to a single, authoritative profile contract.
