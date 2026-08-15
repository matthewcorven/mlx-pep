# model-assessor

Evidence-backed assessment artifacts for tuning local models behind oMLX and preparing a follow-on agent that can benchmark, compare, and operationalize model profiles across tool-using coding and research workflows.

## Current Scope

This repository captures four things:

1. What has already been learned about Gemma 4 12B on oMLX, especially the MTP tradeoffs.
2. What was live-validated against the local oMLX admin and public APIs.
3. A benchmark matrix and a reduced smoke suite for repeatable testing.
4. Prompt templates and a handoff package for the next agent phase.

## Repository Map

- `docs/01-omlx-api-validation.md`: verified local API surface, auth model, and known gaps.
- `docs/02-findings-and-decisions.md`: benchmark takeaways and decisions made so far.
- `docs/03-test-matrix.md`: comprehensive 10-profile matrix covering the target workload classes.
- `docs/04-smoke-suite.md`: reduced 4-profile suite for fast iteration.
- `docs/05-prompt-templates.md`: exact prompts for phase-2 quality evaluation.
- `docs/08-repository-architecture.md`: long-lived architecture and component design.
- `docs/09-operator-workflow.md`: long-lived operator workflow and command reference.
- `docs/10-documentation-status.md`: long-lived map of durable docs versus temporary planning docs.
- `docs/06-next-phase-handoff.md`: requirements and success criteria for the separate agent.
- `config/benchmark_profiles.json`: machine-readable benchmark profile definitions.
- `config/smoke_suite.json`: machine-readable smoke-suite selection.
- `config/prompt_templates.json`: machine-readable evaluation prompt templates.
- `scripts/omlx_bench_harness.py`: Python stdlib harness for profile application and benchmark runs.
- `scripts/run_smoke_suite.sh`: convenience wrapper for the smoke suite.
- `scripts/run_full_matrix.sh`: convenience wrapper for the full matrix.
- `results/README.md`: output structure for generated benchmark evidence.

## Validated Environment Assumptions

- Engine: oMLX local server on `http://127.0.0.1:8000`
- Auth: public API via bearer token; admin API via login plus session cookie
- Benchmark control: admin API, not a first-class `omlx benchmark` CLI command
- Current local version observed during validation: `0.4.4.dev1`

## Quick Start

1. Copy `.env.example` to your shell environment.
1. Set `OMLX_API_KEY` and optionally `OMLX_BASE_URL`.
1. Run the smoke suite:

```bash
./scripts/run_smoke_suite.sh --model-id gemma-4-12B-it-bf16
```

1. Run the full benchmark matrix:

```bash
./scripts/run_full_matrix.sh --model-id gemma-4-12B-it-bf16
```

## TUI Usage

The repo includes a local interactive results browser for reviewing verified assessment runs. The app runs from the TUI project entry point and reads the same `OMLX_BASE_URL` / `OMLX_API_KEY` environment variables as the CLI flows.

Run it from the repo root:

```bash
export OMLX_BASE_URL=http://127.0.0.1:8000
export OMLX_API_KEY=<your-omlx-api-key>

dotnet run --project src/MlxPep.Tui/MlxPep.Tui.csproj
```

Or run the built binary directly:

```bash
cd /Users/core/git/matthewcorven/mlx-pep
dotnet src/MlxPep.Tui/bin/Debug/net10.0/mlx-pep-tui.dll
```

## VS Code Debugging

For debugging in VS Code, add a launch configuration like this:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "mlx-pep TUI",
      "type": "coreclr",
      "request": "launch",
      "program": "${workspaceFolder}/src/MlxPep.Tui/bin/Debug/net10.0/mlx-pep-tui.dll",
      "args": [],
      "cwd": "${workspaceFolder}",
      "console": "integratedTerminal",
      "env": {
        "OMLX_BASE_URL": "http://127.0.0.1:8000",
        "OMLX_API_KEY": "${env:OMLX_API_KEY}"
      }
    }
  ]
}
```

Then start the app from Run and Debug using the `mlx-pep TUI` configuration.

## Next-Phase Workflow Status

The stable, long-lived repository documentation now lives primarily in:

- `docs/08-repository-architecture.md`
- `docs/09-operator-workflow.md`
- `docs/10-documentation-status.md`

The chunked handoff and implementation docs under `docs/06-next-phase-*` and `docs/07-local-model-assessor-architecture.md` are still retained during the current single-instance validation phase, but they are temporary planning material rather than the preferred long-term reference surface.

The repo-local next-phase workflow is implemented and traceable from raw evidence through AI harness recommendation artifacts. The repository now has full live benchmark coverage for the benchmark matrix and live prompt-quality coverage for the short-code-research and short-coding workloads. Long and deep live prompt evaluations remain a practical runtime blocker on the current local service and are called out explicitly in the readiness handoff.

Operators using the repo-local `Local Model Assessor` custom agent should expect the assessment flow to compare both non-MTP and MTP-enabled variants for the same workload whenever the selected model and oMLX support that path. The script layer still performs the deterministic work, but the intended operator experience is agent-led rather than command-led.

- Final readiness handoff: `results/summaries/2026-06-11-gemma-4-12b-it-bf16-next-phase-readiness.md`
- Current normalized evidence: `results/normalized/20260611-062759-gemma-4-12b-it-bf16-normalized/`
- Current recommendation manifest: `results/recommendations/20260611-062759-gemma-4-12b-it-bf16-recommendation/recommendation_manifest.json`
- Current client artifacts: `results/client-configs/20260611-062759-gemma-4-12b-it-bf16-recommendation/`

Use live oMLX for model selection, guarded assistant probing, benchmark execution, and non-dry-run prompt evaluations:

```bash
python3 scripts/next_phase/run_assessment.py \
  --model-id gemma-4-12B-it-bf16 \
  --assistant-model-id gemma-4-12B-it-assistant-bf16 \
  --suite smoke \
  --mtp profile

python3 scripts/next_phase/run_prompt_evals.py \
  --model-id gemma-4-12B-it-bf16 \
  --profile-id short_code_research_tools_mtp_off
```

List the available prompt cases for a specific profile before executing it, so you can choose the exact case IDs for a single, bounded validation pass:

```bash
python3 scripts/next_phase/run_prompt_evals.py \
  --model-id gemma-4-12B-it-bf16 \
  --profile-id deep_research_mtp_off \
  --list-cases

for p in \
  short_code_research_tools_mtp_off \
  long_code_research_tools_mtp_off \
  deep_research_mtp_off; do
  echo "=== $p ==="
  python3 scripts/next_phase/run_prompt_evals.py \
    --model-id gemma-4-12B-it-bf16 \
    --profile-id "$p" \
    --list-cases
done
```

When a stored recommendation manifest declares a multi-instance topology, pass that contract back into the live runner so real assessments target the same separate-port layout that the client artifacts describe:

```bash
python3 scripts/next_phase/run_assessment.py \
  --model-id gemma-4-12B-it-bf16 \
  --suite smoke \
  --mtp profile \
  --topology-manifest results/recommendations/20260611-062759-gemma-4-12b-it-bf16-recommendation/recommendation_manifest.json
```

For long or deep live prompt evaluations on the current local service, use a bounded cap so the run stays practical and the emitted artifacts record the override explicitly:

```bash
python3 scripts/next_phase/run_prompt_evals.py \
  --model-id gemma-4-12B-it-bf16 \
  --profile-id long_code_research_tools_mtp_off \
  --case-id long-research-end-to-end-summary-flow \
  --max-tokens-override 512
```

The practical live catalog also already includes corresponding `*_mtp_on` runs for the Aspire Zig planning prompt, the Next.js commerce architecture prompt, and the Echo Show viability prompt. Swap the example `--profile-id ..._mtp_off` and `--case-id ...-mtp-off` values for the matching `..._mtp_on` and `...-mtp-on` identifiers when you want direct MTP quality comparisons on those same prompts.

Use offline or stored-evidence paths for fixture validation, normalization, reporting, and client-artifact generation:

```bash
python3 scripts/next_phase/run_prompt_evals.py \
  --model-id dry-run-model \
  --profile-id short_code_research_tools_mtp_off \
  --validate-only

python3 scripts/next_phase/generate_recommendation_report.py \
  --model-id gemma-4-12B-it-bf16

python3 scripts/next_phase/generate_client_config_artifacts.py \
  --recommendation-manifest results/recommendations/20260611-062759-gemma-4-12b-it-bf16-recommendation/recommendation_manifest.json
```

## Required Worker Flow And Evidence Gate

The repo now treats the workflow as a strict staged pipeline rather than a loose set of parallel tasks:

1. Run benchmark/probe collection for the selected model and workload set.
2. Run prompt-quality evaluation for the same workloads and case sets.
3. Verify each workload has both benchmark evidence and prompt-quality evidence before using data downstream.
4. Only then run `generate_recommendation_report.py`.
5. Only then run `generate_client_config_artifacts.py`.

This is not optional. The recommendation and client-artifact scripts now refuse to emit downstream artifacts if required evidence is missing. In practical terms, a report should be understood as a checkpoint artifact, not a first-class source of truth for an unvalidated model run.

Read the readiness handoff before treating the current recommendations as complete. The current repo outputs now cover all five benchmark workloads and provide actionable ranked recommendations, but confidence remains low where long and deep prompt-quality evidence is still missing.

## Guiding Principle

The next phase should optimize for operator decisions, not raw benchmark volume. Every artifact here is aimed at producing evidence that can be translated into:

- per-model oMLX profiles
- optional MTP assistant-model usage decisions
- one per-run AI harness reference table covering VS Code, VS Code Insiders, Claude Code, GitHub Copilot CLI, and OpenCode terminology
- per-workload recommended oMLX settings that remain operator-applied rather than auto-written into downstream clients
- explicit multi-instance hosting guidance when recommended workloads cannot share one oMLX-side configuration
