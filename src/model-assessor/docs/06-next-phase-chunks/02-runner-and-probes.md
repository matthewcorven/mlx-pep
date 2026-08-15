# Chunk 2: Reusable Runner And Assistant Probes

## Purpose

Turn the current one-profile benchmark harness into a reusable deterministic runner that can discover models, apply profile settings, run benchmark profiles, and probe assistant/MTP compatibility safely.

## Context To Read

- `docs/06-next-phase-handoff.md`
- `docs/06-next-phase-chunks/01-agent-architecture.md`
- `docs/06-next-phase-chunks/shared-contracts.md`
- `docs/01-omlx-api-validation.md`
- `docs/03-test-matrix.md`
- `docs/04-smoke-suite.md`
- `results/README.md`
- `scripts/omlx_bench_harness.py`
- `scripts/run_smoke_suite.sh`
- `scripts/run_full_matrix.sh`

## Scope

This chunk owns benchmark and capability evidence collection. It should not implement prompt-quality evaluation, final report interpretation, or client config outputs except for metadata needed by later chunks.

Current repo status: the manifest and client-artifact contract now also drives the live runner path. `run_assessment.py` can derive a topology from the selected profiles or consume an existing `instance_topology` block so real runs honor separate-port multi-instance hosting when workload settings diverge.

## Required Outputs

- A reusable runner interface that supports:
  - target model ID
  - optional assistant model ID
  - explicit MTP on/off
  - workload-profile selection
  - smoke-suite and full-matrix execution
- The runner CLI and `run_manifest.json` output described in `shared-contracts.md`, or a documented compatible superset.
- oMLX discovery/probe logic for:
  - model inventory
  - profile fields
  - selected-model settings
  - assistant candidate inventory
  - guarded assistant compatibility checks
- Assistant probe artifacts using the decision-chain schema in `shared-contracts.md`.
- Any Hugging Face/model-card enrichment metadata needed for candidates, clearly marked as enrichment and never as compatibility proof.
- Output artifacts that preserve raw requests, responses, settings, probe outcomes, and benchmark results.
- Wrapper scripts or CLI options that keep common runs easy for operators.

## Acceptance Criteria

- Existing smoke and full-matrix workflows still work or are replaced by documented equivalent commands.
- The public runner interface includes target model ID, optional assistant model ID, MTP mode, profile selection, suite selection, base URL, API key, and output directory.
- The runner uses oMLX inventory and guarded probes as the authority for assistant availability and compatibility.
- Hugging Face/model-card information, if included, is enrichment only and never treated as proof of local compatibility.
- Probe result artifacts explicitly record candidate source, oMLX inventory check, probe attempt status, outcome, fallback action, and evidence paths.
- Unsupported assistant paths continue target-model-only execution and record the unsupported state as evidence.
- Settings updates preserve the validated read/merge/full-body `PUT` behavior unless sparse update behavior is revalidated and documented.
- Results include enough metadata for later normalization by model ID, assistant model ID, workload, profile ID, MTP state, and settings.
- The implementation gracefully handles missing or unsupported speculative fields per model.
- Auth uses `OMLX_API_KEY` or `--api-key`; each runner invocation establishes an admin login session with cookie handling, matching the validated admin API behavior.
- Benchmark execution, assistant probing, and metric capture are deterministic script work, not AI reasoning.

## Definition Of Done

- Runner code and wrapper updates are committed to files in the repo working tree.
- Focused validation passes, at minimum Python syntax checks and shell syntax checks for changed scripts.
- If live oMLX is unavailable, commands are documented and dry-run or syntax validation is performed instead.
- Result artifact layout remains compatible with `results/README.md`, or that doc is updated.
- `shared-contracts.md` remains accurate for runner and probe outputs, or is updated with any intentional compatible change.
- The final response lists changed files, validation commands, and any live-probe limitations.

## Launch Prompt

You are implementing Chunk 2 of the model-assessor next phase. Read `docs/06-next-phase-handoff.md`, `docs/06-next-phase-chunks/01-agent-architecture.md`, `docs/06-next-phase-chunks/shared-contracts.md`, and `docs/06-next-phase-chunks/02-runner-and-probes.md`. Implement the reusable benchmark runner and oMLX assistant-probe path only. Preserve deterministic behavior and existing API assumptions. Do not use LLM reasoning for probe decisions, settings application, benchmark execution, or metric capture. Validate scripts and report changed files plus any live oMLX limitations.
