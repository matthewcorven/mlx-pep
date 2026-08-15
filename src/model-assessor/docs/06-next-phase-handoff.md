# Next-Phase Handoff

This document is for the separate agent that will take over the next phase.

## Mission

Create a custom AI agent and its supporting skills, scripts, and tools so a human operator can assess any given model and optional MTP assistant model using oMLX as the inference engine.

The final goal is to produce evidence-backed recommendations for:

- optimal oMLX model profiles
- optional assistant-model usage for MTP
- VS Code configuration per use case
- Claude Code configuration per use case
- GitHub Copilot CLI configuration per use case
- OpenCode configuration per use case

## Inputs Available From This Repository

- validated local oMLX API assumptions
- a comprehensive workload and benchmark matrix
- a smoke suite for fast verification
- representative prompt-quality templates
- a Python harness and shell wrappers for benchmark automation

## Requirements For The Next Agent

### 1. Model selection support

The next phase must support:

- a target model ID
- an optional assistant model ID
- explicit MTP on or off configuration
- workload-profile selection

## Chunk-0 Decisions

These decisions resolve the setup questions that should be fixed before implementation begins.

### Custom agent shape

Build a repo-local VS Code custom agent named `Local Model Assessor` and its supporting files. The customization files should live in this repository so they travel with the evidence pack and can be reviewed with the implementation.

Deterministic work should stay deterministic and script-based. AI should primarily be used for:

- preparing for new model testing, including model-card research and candidate enrichment from Hugging Face or related sources
- interpreting benchmark and evaluation evidence
- producing operator-facing recommendations and tradeoff summaries

The agent should orchestrate scripts and generated artifacts rather than replacing reproducible benchmark, evaluation, or report-generation logic with freeform reasoning.

### AI harness reference outputs

Generate all of the following for VS Code, Claude Code, GitHub Copilot CLI, and OpenCode recommendations:

- a human-readable Markdown report
- machine-readable JSON recommendation manifests
- one per-run AI harness reference table with rows for VS Code, VS Code Insiders, Claude Code, GitHub Copilot CLI, and OpenCode
- the exact official terminology, object names, keys, settings, environment variables, and selectors each harness uses for local or custom model configuration
- recommended values from the assessment beside those harness terms so operators can configure the harness manually
- an explicit declaration when ideal workload recommendations require more than one simultaneously hosted model instance because MTP or other oMLX-side settings diverge across workloads

### First validation target

Use `gemma-4-12B-it-bf16` as the first end-to-end validation target because it matches the current docs and the first real benchmark summary.

### Assistant-model handling

Probe available assistant models during the next phase. Do not assume assistant compatibility from public listings alone. The implementation should discover candidates from oMLX, attempt guarded compatibility checks, and record unsupported assistant paths as evidence rather than treating them as hard failures.

Assistant availability should be oMLX-confirmed. Hugging Face and model-card sources may be used to enrich candidates, understand model families, identify plausible assistant pairings, and surface license or architecture warnings, but they must not be treated as proof that a model is locally available or compatible. Assistant recommendations require oMLX inventory evidence and guarded oMLX probe evidence.

If assistant probing fails or the selected assistant path is unsupported, continue with target-model-only benchmarking and evaluation. The report should clearly record that assistant usage was attempted and unsupported for that model or configuration.

### Evaluation fixtures

Use synthetic fixtures for prompt-quality evaluation. Fixtures should include a mini fixture repository, prompt cases, and expected answers. They should be controlled and repeatable so profile comparisons are not confounded by changing repository state or external codebase differences.

### Recommendation style

Reports should provide ranked recommendations per workload, confidence or caveats, and tradeoff detail. Do not require numeric win/tie/regression thresholds in the first implementation. The output should be decisive enough for an operator to act on while preserving the evidence behind close calls, and numeric thresholds can be added later after enough runs establish useful bands.

### 2. Evidence collection

The agent must collect:

- benchmark metrics
- settings used for each run
- prompt-quality outputs
- a structured summary comparing profiles

### 3. Operator-facing outputs

The system must generate artifacts that let a human operator choose:

- the best model profile for each use case
- whether MTP should be enabled for each use case
- which row of the AI harness reference table to use for VS Code, VS Code Insiders, Claude Code, GitHub Copilot CLI, or OpenCode
- whether one shared hosted model instance is sufficient or whether multiple concurrently hosted instances are required to satisfy the recommended workload mix

### 4. Reusability

The next-phase implementation must not be hardcoded only for Gemma 4 12B. It should work for any oMLX-exposed model and gracefully handle model-specific capability differences.

## Expected Deliverables From The Next Phase

1. A custom AI agent specification.
2. Any required skills, tools, or support scripts.
3. A way to benchmark a target model with or without an assistant model.
4. A report generator that converts raw evidence into profile recommendations.
5. AI harness reference artifacts per workload class and supported harness.

## Chunk Dispatch

The next phase should be executed as chunked work. Give every implementation agent this handoff plus the specific chunk document assigned to it.

Primary dispatch index: `docs/06-next-phase-chunks/00-dispatch-index.md`

Shared implementation contracts: `docs/06-next-phase-chunks/shared-contracts.md`

Chunk 1 architecture output for later chunks: `docs/07-local-model-assessor-architecture.md`

| Chunk | Work Document | Purpose |
| --- | --- | --- |
| 1 | `docs/06-next-phase-chunks/01-agent-architecture.md` | Define `Local Model Assessor`, repo-local agent scaffold, architecture, and shared contracts. |
| 2 | `docs/06-next-phase-chunks/02-runner-and-probes.md` | Build the reusable deterministic benchmark runner and assistant/MTP probe path. |
| 3 | `docs/06-next-phase-chunks/03-synthetic-evaluations.md` | Build synthetic fixtures and prompt-quality evaluation runner. |
| 4 | `docs/06-next-phase-chunks/04-normalization-and-reporting.md` | Normalize evidence and generate ranked recommendation reports/manifests. |
| 5 | `docs/06-next-phase-chunks/05-client-config-artifacts.md` | Generate VS Code, Claude Code, GitHub Copilot CLI, and OpenCode config artifacts. |
| 6 | `docs/06-next-phase-chunks/06-end-to-end-validation.md` | Validate the full workflow and produce final readiness handoff. |

Before any implementation chunk starts, the assigned agent must read the shared contracts file and treat its artifact paths, schemas, traceability fields, and deterministic/AI responsibility boundaries as part of that chunk's acceptance criteria.

## Known Constraints

- oMLX benchmark automation currently depends on the local admin HTTP API rather than a dedicated benchmark CLI.
- Model compatibility for assistant or speculative paths cannot be inferred purely from public listings.
- Some model features are experimental and may affect benchmark upload behavior.

## Recommended Next-Phase Sequence

1. turn the current benchmark harness into a reusable test runner
2. add support for prompt-quality evaluation runs
3. add assistant-model and capability detection logic
4. create the custom agent definition and supporting skills
5. generate operator-facing recommendation outputs

## Success Criteria

The phase is successful when a human operator can:

- choose a model and optional assistant model
- run repeatable benchmarks and quality evaluations
- receive evidence-backed recommended profiles by use case
- receive one reference-table row per workload and supported AI harness, using the harness's own terminology for custom/local model configuration
- understand when the recommendation set requires multiple simultaneously hosted model instances because workloads disagree on MTP or other oMLX-side settings
- apply the resulting settings to oMLX and downstream client tools with minimal manual work
