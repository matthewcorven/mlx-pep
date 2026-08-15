# Next-Phase Shared Contracts

These contracts apply to all implementation chunks. Each chunk agent should read this file together with `docs/06-next-phase-handoff.md` and its assigned chunk document.

## Repo-Local Agent Files

Chunk 1 should create the repo-local VS Code custom agent scaffold at these paths unless it documents a stronger repo-local convention before implementation:

- `.github/agents/local-model-assessor.agent.md`
- `.github/instructions/local-model-assessor.instructions.md`
- `.github/prompts/local-model-assessor-run.prompt.md`
- `docs/07-local-model-assessor-architecture.md`

The agent name must be `Local Model Assessor`. Supporting files must describe deterministic script responsibilities, AI-assisted responsibilities, model-card enrichment, and evidence interpretation boundaries.

## Deterministic And AI-Assisted Boundaries

The following work must be deterministic and script-based:

- oMLX model discovery and assistant probing
- benchmark execution and settings application
- prompt-quality evaluation execution and raw output capture
- metric normalization
- recommendation manifest generation
- client artifact generation from structured manifests

AI may be used for:

- model-card and Hugging Face candidate enrichment
- architecture review
- interpreting evidence and caveats
- drafting operator-facing recommendation text from structured evidence

AI must not be the authority for local model availability, assistant compatibility, raw metric extraction, or generated JSON validity.

## Validation Target Clarification

- Chunks 1, 3, 4, and 5 should validate with synthetic/sample fixtures and do not require live oMLX.
- Chunk 2 should validate syntax and dry-run/listing behavior without live oMLX where possible; live oMLX probes are optional unless the operator provides the service and credentials.
- Chunk 6 should run the smoke suite against `gemma-4-12B-it-bf16` when live oMLX is available. The full matrix is optional unless the operator explicitly requests it.
- If live oMLX or `gemma-4-12B-it-bf16` is unavailable, Chunk 6 should document exact commands, required environment, and expected artifact paths rather than blocking the repository handoff.

## Artifact Layout

New artifacts should use these paths unless a chunk updates `results/README.md` with a coordinated replacement:

- Raw benchmark runs: `results/runs/<run_id>/`
- Assistant probes: `results/runs/<run_id>/assistant_probe.json`
- Prompt-quality evaluations: `results/evaluations/<evaluation_run_id>/`
- Normalized evidence: `results/normalized/<normalization_id>/`
- Recommendation manifests: `results/recommendations/<recommendation_id>/recommendation_manifest.json`
- Recommendation reports: `results/summaries/<recommendation_id>.md`
- Client artifacts: `results/client-configs/<recommendation_id>/`

Use stable IDs instead of only timestamps. A recommended ID shape is `<YYYYMMDD-HHMMSS>-<model-slug>-<profile-or-suite>`. If a UUID is used, also include human-readable model and suite metadata.

## Traceability Fields

Every downstream artifact should include the relevant identifiers below:

```json
{
  "schema_version": "1.0",
  "run_id": "string|null",
  "evaluation_run_id": "string|null",
  "normalization_id": "string|null",
  "recommendation_id": "string|null",
  "created_at": "ISO-8601 timestamp",
  "model_id": "string",
  "assistant_model_id": "string|null",
  "profile_id": "string|null",
  "workload": "string|null",
  "mtp_enabled": "boolean|null",
  "source_paths": ["relative/path/from/repo/root"]
}
```

Chunk 4 must preserve links back to raw benchmark and evaluation artifacts. Chunk 5 must preserve links back to the recommendation manifest it consumed.

## Runner Interface Contract

Chunk 2 should expose a deterministic CLI with this shape or document any compatible superset:

```bash
python3 scripts/next_phase/run_assessment.py \
  --model-id <model-id> \
  [--assistant-model-id <assistant-model-id>] \
  [--mtp on|off|profile] \
  [--profile-id <profile-id>] \
  [--suite smoke|full|single] \
  [--topology-manifest <path-to-recommendation-or-topology-json>] \
  [--base-url <url>] \
  [--api-key <key>] \
  [--results-dir results/runs]
```

Auth should read `OMLX_API_KEY` when `--api-key` is omitted. Each invocation should establish an admin session with login plus cookie handling, matching the validated API behavior.

When `--topology-manifest` is supplied, the runner should consume the existing `instance_topology` contract from a recommendation manifest or topology document and route each selected profile to the mapped `base_url` instead of assuming one shared endpoint for the whole run.

The runner should produce `run_manifest.json` under `results/runs/<run_id>/` with at least:

```json
{
  "schema_version": "1.0",
  "run_id": "string",
  "created_at": "ISO-8601 timestamp",
  "model_id": "string",
  "assistant_model_id": "string|null",
  "suite": "smoke|full|single",
  "profile_ids": ["string"],
  "mtp_mode": "on|off|profile",
  "base_url": "string",
  "instance_topology": {},
  "profile_execution_plan": [],
  "artifact_paths": {
    "model_inventory": "relative/path",
    "profile_fields": "relative/path",
    "settings_requests": ["relative/path"],
    "benchmark_results": ["relative/path"],
    "assistant_probe": "relative/path|null"
  },
  "status": "success|partial|failed",
  "errors": ["string"]
}
```

## Assistant Probe Contract

Assistant availability is oMLX-confirmed. Hugging Face and model-card data are enrichment only.

Probe artifacts should record the decision chain:

```json
{
  "schema_version": "1.0",
  "run_id": "string",
  "model_id": "string",
  "assistant_model_id": "string|null",
  "candidate_sources": [
    {
      "source": "omlx_inventory|hugging_face|operator_supplied",
      "candidate_id": "string",
      "notes": "string|null"
    }
  ],
  "omlx_inventory_check": "found|not_found|not_attempted",
  "probe_attempted": true,
  "probe_status": "supported|unsupported|failed|timeout|not_attempted",
  "failure_reason": "string|null",
  "fallback_action": "none|target_model_only",
  "evidence_paths": ["relative/path"]
}
```

Unsupported assistant paths should continue target-only execution and should not be treated as harness failure unless the requested chunk specifically tests failure handling.

## Prompt-Quality Evaluation Contract

Chunk 3 should expose a deterministic CLI with this shape or document any compatible superset:

```bash
python3 scripts/next_phase/run_prompt_evals.py \
  --model-id <model-id> \
  [--assistant-model-id <assistant-model-id>] \
  --profile-id <profile-id> \
  --cases config/evaluation_cases.json \
  [--fixture-root fixtures/synthetic_repo] \
  [--results-dir results/evaluations]
```

Evaluation runs should produce `evaluation_manifest.json` with:

```json
{
  "schema_version": "1.0",
  "evaluation_run_id": "string",
  "fixture_version": "string",
  "fixture_hash": "sha256 string",
  "model_id": "string",
  "assistant_model_id": "string|null",
  "profile_id": "string",
  "mtp_enabled": true,
  "case_result_paths": ["relative/path"],
  "status": "success|partial|failed"
}
```

Expected-answer metadata should use required facts, forbidden claims, and quality signals rather than exact wording:

```json
{
  "case_id": "string",
  "workload": "short_code_research_tools|long_code_research_tools|short_coding|long_coding|deep_research",
  "prompt_template_id": "string",
  "placeholder_values": {},
  "required_facts": [
    {"fact": "string", "importance": "must_have|strong|nice_to_have"}
  ],
  "forbidden_claims": [
    {"claim": "string", "severity": "critical|warning"}
  ],
  "quality_signals": [
    {"signal": "string", "detectable": "automatic|manual", "description": "string"}
  ],
  "manual_review_notes": "string|null"
}
```

The first fixture set should be small: at least one case per workload class, text-only, under 5 MB, with a fixture version and hash that change whenever fixture files, prompt cases, or expected-answer definitions change.

## Normalization And Recommendation Contract

Chunk 4 should consume run manifests and evaluation manifests, tolerate missing evidence, and mark incomplete comparisons explicitly.

Correlation keys:

- `model_id`
- `assistant_model_id`
- `profile_id`
- `workload`
- `mtp_enabled`
- `settings` or a settings hash

Recommendation manifests should use this shape:

```json
{
  "schema_version": "1.0",
  "recommendation_id": "string",
  "created_at": "ISO-8601 timestamp",
  "model_id": "string",
  "assistant_model_id": "string|null",
  "source_run_ids": ["string"],
  "source_evaluation_run_ids": ["string"],
  "recommendations": [
    {
      "workload": "string",
      "rank": 1,
      "profile_id": "string",
      "mtp_recommended": true,
      "assistant_recommended": false,
      "confidence": "high|medium|low|insufficient_evidence",
      "speed_summary": "string",
      "quality_summary": "string",
      "tradeoffs": ["string"],
      "caveats": ["string"],
      "source_paths": ["relative/path"]
    }
  ],
  "instance_topology": {
    "instance_mode": "single|multi",
    "instance_count": 1,
    "instances": [
      {
        "instance_id": "string",
        "port": 8000,
        "base_url": "http://127.0.0.1:8000",
        "workload": "string",
        "profile_id": "string|null",
        "mtp_enabled": true,
        "assistant_model_id": "string|null",
        "reason": "string"
      }
    ],
    "workload_to_instance": {"string": "string"},
    "instance_topology_summary": "string"
  },
  "missing_evidence": ["string"]
}
```

Ranking without numeric thresholds should be evidence-led and conservative:

- prefer profiles with clearly better quality when speed is close or conflicting
- prefer lower latency/TTFT for short interactive workloads when quality is comparable
- prefer sustained throughput and long-context stability for long coding and deep research when quality is comparable
- present close calls as ranked with low or medium confidence and explicit caveats rather than inventing a precise margin

## Client Artifact Contract

Chunk 5 must generate recommendation artifacts for operator review. It must not modify real user, workspace, or client configuration files automatically.

Client artifacts should be written under `results/client-configs/<recommendation_id>/`:

- `README.md`: operator guidance and caveats
- `client_recommendations.json`: machine-readable client mapping
- `ai-harness-reference.md`: one operator-facing reference table for all supported harnesses
- `unsupported-settings.md`: settings that cannot be directly represented by a client

Chunk 5 outputs must treat supported AI harnesses as first-class recommendation targets rather than simple session-label examples. The generated machine-readable artifact must include one row per workload recommendation and supported AI harness, plus enough metadata to tell the operator whether a single hosted model instance can satisfy all selected workloads.

If the top-ranked workload recommendations disagree on MTP state, assistant-model usage, or any other oMLX-side setting that cannot be reconciled into one hosted model instance, the generated artifacts must explicitly declare that multiple simultaneously hosted instances are required. In this iteration the declaration is advisory only; future software may automate instance management.

The reference table must avoid credentials, use placeholders for secrets, and use the `model_id` and profile data from the recommendation manifest rather than hardcoded Gemma defaults. It should lead with the exact terminology from official harness documentation, then place the recommended values next to those terms for operator use.

`client_recommendations.json` should expose at least these additional logical sections even if the exact field names evolve during implementation:

- supported harness list including `vscode`, `claude_code`, `github_copilot_cli`, and `opencode`
- per-workload ranked recommendations as today
- AI harness reference rows covering VS Code, VS Code Insiders, Claude Code, GitHub Copilot CLI, and OpenCode terminology and recommended values
- an instance-topology summary describing whether one shared hosted instance is sufficient or whether multiple concurrently hosted instances are required, including the divergent oMLX-side reasons
- unsupported or manual-only settings and the manual application path

## Results Documentation Coordination

If any chunk changes artifact paths, schemas, or retention expectations, it must update `results/README.md` with:

- path
- purpose
- owning chunk
- schema or schema file location
- traceability fields

Chunks should not independently introduce conflicting result layouts.
