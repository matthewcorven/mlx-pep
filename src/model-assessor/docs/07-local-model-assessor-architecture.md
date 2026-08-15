# Local Model Assessor Architecture

This file remains the Chunk 1 handoff architecture record. For the long-lived architecture reference, use `docs/08-repository-architecture.md`. Keep this file until the current validation and cleanup phase is complete.

This document is the Chunk 1 architecture handoff for the repo-local `Local Model Assessor` custom agent and the stable interfaces that later chunks should implement.

## Purpose

`Local Model Assessor` is a repo-local VS Code custom agent that helps a human operator assess any oMLX-exposed local model, with an optional assistant model and explicit MTP state, while keeping reproducible work inside deterministic scripts.

The design is model-agnostic. `gemma-4-12B-it-bf16` is the first validation target because it matches the current evidence pack, not because later chunks should special-case it.

## Chunk 1 Outputs

- `.github/agents/local-model-assessor.agent.md`
- `.github/instructions/local-model-assessor.instructions.md`
- `.github/prompts/local-model-assessor-run.prompt.md`
- `docs/07-local-model-assessor-architecture.md`

## Operating Model

The operator chooses a target model and optional assistant model, then the repo-local prompt routes work to `Local Model Assessor`. The agent reads the repo contracts, invokes deterministic scripts when they exist, and uses AI only for bounded enrichment and evidence interpretation.

## Responsibility Split

| Area | Deterministic Owner | AI-Assisted Owner | Authority |
| --- | --- | --- | --- |
| Local model inventory | oMLX API calls and emitted snapshots | none | oMLX |
| Assistant candidate discovery | oMLX inventory, guarded probes | candidate enrichment and family research | oMLX for availability, Hugging Face for enrichment |
| Benchmark execution | repository scripts and emitted artifacts | none | oMLX plus script output |
| Prompt-quality execution | repository scripts and emitted artifacts | none | script output |
| Metric normalization | repository scripts and manifests | none | script output |
| Recommendation text | structured evidence inputs | caveats, tradeoffs, operator-facing narrative | recommendation manifest plus cited evidence |
| AI harness reference table | repository scripts from structured manifests and official-source terminology | wording review only | recommendation manifest plus official harness docs |

## Authority Rules

### oMLX authority

oMLX is authoritative for:

- local model inventory
- assistant-model availability
- assistant compatibility and guarded probe outcomes
- admin settings, profile fields, and merged settings requests
- benchmark progress and final benchmark results

### Hugging Face and model-card enrichment

Hugging Face and model-card sources may be used to:

- enrich model-family metadata
- identify plausible assistant candidates for probing
- surface architecture, context-window, or license caveats
- explain why an oMLX-supported or unsupported path might exist

Hugging Face and model-card sources must not be used as proof that a model is locally available, benchmarkable, or assistant-compatible.

## Operator Inputs

Every end-to-end assessment flow should support these inputs:

| Input | Required | Notes |
| --- | --- | --- |
| `model_id` | yes | target model exposed by oMLX |
| `assistant_model_id` | no | optional candidate assistant model |
| `mtp` | yes | `on`, `off`, or `profile` |
| `profile_id` | conditional | use for exact benchmark profile selection |
| `workload` | conditional | use when selecting a workload class instead of an exact profile |
| `suite` | yes | `smoke`, `full`, or `single` |
| `base_url` | no | defaults to local oMLX base URL |
| `api_key` | no | falls back to `OMLX_API_KEY` |

At least one of `profile_id` or `workload` must be supplied for focused execution.

## Existing Deterministic Entry Points

Chunk 1 does not replace the current harness. Later chunks should build on these files:

- `scripts/omlx_bench_harness.py`: current deterministic benchmark harness over the admin HTTP API
- `scripts/run_smoke_suite.sh`: current smoke wrapper using `config/smoke_suite.json`
- `scripts/run_full_matrix.sh`: current full-matrix wrapper using `config/benchmark_profiles.json`
- `config/benchmark_profiles.json`: current workload and MTP profile definitions
- `config/smoke_suite.json`: current smoke subset
- `config/prompt_templates.json`: current workload prompt template seed material

These files are the baseline that later chunks should extend, not bypass.

## Reserved Interfaces For Later Chunks

The exact schemas live in `docs/06-next-phase-chunks/shared-contracts.md`. This section names the stable implementation surfaces that later chunks should target.

### Chunk 2: Runner and probes

- Reserved CLI: `python3 scripts/next_phase/run_assessment.py ...`
- Required outputs: `results/runs/<run_id>/run_manifest.json` and `assistant_probe.json`
- Required behavior: log in to the admin API with session cookies, read current settings, merge overrides, perform full-body settings `PUT`, run the benchmark, fetch final results, and continue target-only execution when assistant probing is unsupported.

### Chunk 3: Prompt-quality evaluations

- Reserved CLI: `python3 scripts/next_phase/run_prompt_evals.py ...`
- Required outputs: `results/evaluations/<evaluation_run_id>/evaluation_manifest.json` and per-case result artifacts
- Required behavior: use synthetic, repeatable fixtures and keep required facts or forbidden claims separate from exact wording.

### Chunk 4: Normalization and recommendation generation

- Canonical inputs: run manifests and evaluation manifests
- Required outputs: normalized evidence under `results/normalized/<normalization_id>/`, recommendation manifests under `results/recommendations/<recommendation_id>/recommendation_manifest.json`, and operator-facing summaries under `results/summaries/`
- Required behavior: rank profiles conservatively, preserve traceability, and mark missing evidence explicitly.

### Chunk 5: Client configuration artifacts

- Canonical input: recommendation manifest
- Required outputs: `results/client-configs/<recommendation_id>/README.md`, `client_recommendations.json`, one `ai-harness-reference.md` table covering VS Code, VS Code Insiders, Claude Code, GitHub Copilot CLI, and OpenCode, plus `unsupported-settings.md`
- Required behavior: emit recommendation artifacts only. Do not mutate live user, workspace, AI harness, or oMLX settings. Declare when the recommended workload mix requires multiple simultaneously hosted model instances because oMLX-side settings diverge.

## Artifact Layout

Later chunks should write outputs under these directories:

- `results/runs/<run_id>/`
- `results/evaluations/<evaluation_run_id>/`
- `results/normalized/<normalization_id>/`
- `results/recommendations/<recommendation_id>/`
- `results/client-configs/<recommendation_id>/`
- `results/summaries/`

Recommended identifier shape: `<YYYYMMDD-HHMMSS>-<model-slug>-<profile-or-suite>`.

Each downstream artifact should preserve the traceability fields defined in `docs/06-next-phase-chunks/shared-contracts.md`, including `schema_version`, `run_id`, `evaluation_run_id`, `normalization_id`, `recommendation_id`, `created_at`, `model_id`, `assistant_model_id`, `profile_id`, `workload`, `mtp_enabled`, and `source_paths`.

## Model-Tiering Guidance

Use the execution tier that matches the task:

- Frontier or large-context model: architecture review, close-call evidence interpretation, final recommendation text, and operator-facing tradeoff synthesis.
- Smaller model: bounded extraction, fixture drafting, terse table fill, or low-risk prose cleanup.
- Deterministic scripts: any work that produces settings, metrics, manifests, rankings, hashes, or client artifacts.

The custom agent should default to the deterministic path whenever the task crosses from interpretation into artifact generation.

## Failure Handling Rules

- Unsupported assistant-model paths are evidence, not harness failure.
- If assistant probing fails, the system should record the failure reason and continue with target-model-only execution.
- Missing benchmark fields or partial evaluation evidence should be recorded in manifests, not silently dropped.
- If live oMLX is unavailable, later chunks should still validate syntax, dry-run behavior, fixture integrity, and artifact emission with sample data where the shared contracts allow it.

## Validation Expectations

Chunk 1 requires no live oMLX validation. Validation for this chunk is limited to scaffold correctness, markdown health where tooling is available, and contract consistency with:

- `docs/06-next-phase-handoff.md`
- `docs/06-next-phase-chunks/shared-contracts.md`
- `docs/01-omlx-api-validation.md`
- `docs/02-findings-and-decisions.md`
- `results/README.md`

Chunk 2 is the next implementation dependency.
