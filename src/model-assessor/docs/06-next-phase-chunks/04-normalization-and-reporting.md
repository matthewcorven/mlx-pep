# Chunk 4: Normalization And Recommendation Reporting

## Purpose

Convert raw benchmark and prompt-quality evidence into operator-facing profile recommendations with ranked workload guidance, caveats, and tradeoffs.

## Context To Read

- `docs/06-next-phase-handoff.md`
- `docs/06-next-phase-chunks/01-agent-architecture.md`
- `docs/06-next-phase-chunks/shared-contracts.md`
- `docs/06-next-phase-chunks/02-runner-and-probes.md`
- `docs/06-next-phase-chunks/03-synthetic-evaluations.md`
- `results/README.md`
- `results/summaries/2026-06-11-gemma4-practical-default-first-real-run.md`

## Scope

This chunk owns normalization, comparison, and recommendation reporting. It should not implement benchmark execution, prompt-evaluation execution, or AI harness reference generation beyond producing structured recommendation data needed by Chunk 5.

## Required Outputs

- A normalizer that can read the run and evaluation manifests described in `shared-contracts.md` and extract comparable metrics.
- A report generator that produces Markdown summaries with:
  - per-workload ranked recommendations
  - MTP on/off comparison
  - assistant-model outcome summary
  - quality-evaluation summary
  - caveats and tradeoffs
- A machine-readable recommendation manifest using the schema in `shared-contracts.md`, consumed by AI harness reference generation.
- Documentation for report generation commands and expected inputs.

## Deterministic Entry Point

Chunk 4 should expose a deterministic CLI entry point for normalization and reporting:

```bash
python3 scripts/next_phase/generate_recommendation_report.py \
  --model-id <model-id> \
  [--assistant-model-id <assistant-model-id>] \
  [--run-id <run-id>] \
  [--evaluation-run-id <evaluation-run-id>] \
  [--runs-dir results/runs] \
  [--evaluations-dir results/evaluations] \
  [--normalized-dir results/normalized] \
  [--recommendations-dir results/recommendations] \
  [--summaries-dir results/summaries]
```

Expected inputs are the Chunk 2 `run_manifest.json` artifacts, the Chunk 3 `evaluation_manifest.json` artifacts, and any linked per-profile benchmark or scoring JSON those manifests reference.

## Acceptance Criteria

- Normalized outputs include TTFT, TPOT, generation TPS, prefill TPS, end-to-end latency, total throughput, peak memory when available, and explicit missing-data markers when not available.
- Comparisons are grouped by workload class, model ID, optional assistant model ID, profile ID, MTP state, and settings.
- Recommendations are ranked using the conservative evidence-led guidance in `shared-contracts.md` and do not require numeric win/tie/regression thresholds in the first implementation.
- Close calls preserve caveats instead of overstating confidence.
- Assistant-probe failures or unsupported paths are surfaced clearly.
- Prompt-quality evidence is incorporated separately from benchmark metrics so speed does not silently override usefulness.
- Generated Markdown follows the existing summary style while making clear which parts were generated from evidence.
- All metric normalizations, profile mappings, and report templates are parameterized by `model_id` and do not hardcode Gemma except in clearly labeled examples.
- Normalization and manifest emission are deterministic script work. AI may draft final explanatory prose from structured evidence, but must not extract raw metrics or validate JSON.
- Missing benchmark or evaluation evidence is represented explicitly rather than silently dropping a workload or assistant path.

## Definition Of Done

- Normalizer and report generator files exist in the repo.
- At least one report can be generated from fixture/sample/backfilled evidence without requiring a live benchmark.
- JSON outputs validate as JSON and Markdown outputs are readable.
- `results/README.md` is updated if new summary or manifest conventions are introduced.
- `shared-contracts.md` remains accurate for normalization and recommendation outputs, or is updated with any intentional compatible change.
- The final response lists changed files, validation commands, and sample output paths.

## Launch Prompt

You are implementing Chunk 4 of the model-assessor next phase. Read `docs/06-next-phase-handoff.md`, Chunk 1, Chunk 2, Chunk 3, `docs/06-next-phase-chunks/shared-contracts.md`, and `docs/06-next-phase-chunks/04-normalization-and-reporting.md`. Build the normalizer and recommendation report generator only. Use existing or sample evidence as needed. Do not generate final AI harness reference artifacts; produce the structured manifest that Chunk 5 will consume. Keep metric extraction, normalization, JSON generation, and manifest validation deterministic. Validate outputs and report changed files.
