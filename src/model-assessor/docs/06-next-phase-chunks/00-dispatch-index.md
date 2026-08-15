# Next-Phase Chunk Dispatch Index

Use this index with `docs/06-next-phase-handoff.md` and `docs/06-next-phase-chunks/shared-contracts.md`. Each implementation agent should receive the main handoff, the shared contracts, and exactly one chunk document unless it is explicitly assigned integration work.

For a large-context frontier-model orchestrator that will coordinate all chunks and second-round validation, use `docs/06-next-phase-chunks/master-orchestrator-prompt.md`.

## Execution Model

The next phase is intentionally chunked. Deterministic work should be implemented in scripts and config files. AI should be used for planning, model-card research, evidence interpretation, and final recommendation judgment.

## Current Status Snapshot

The manifest, client-artifact path, and live runner path now share the same topology contract: recommendation manifests carry `instance_topology`, client artifact generation consumes that contract, and `run_assessment.py` can execute per-profile runs against topology-selected oMLX instances on separate ports when workload settings diverge.

Use frontier or large-context models for architecture, final synthesis, and close-call interpretation. Use smaller models or deterministic scripts for bounded extraction, table generation, fixture checks, and manifest emission.

## Chunk Order

| Chunk | Document | Primary Owner Model | Depends On | Unlocks |
| --- | --- | --- | --- | --- |
| 1 | `01-agent-architecture.md` | Frontier or large-context | Main handoff | All implementation chunks |
| 2 | `02-runner-and-probes.md` | Deterministic script work, frontier review | Chunk 1 | Benchmark evidence collection |
| 3 | `03-synthetic-evaluations.md` | Deterministic script work, smaller model acceptable for drafts | Chunk 1 | Prompt-quality evidence collection |
| 4 | `04-normalization-and-reporting.md` | Deterministic script work, frontier review | Chunks 2 and 3 | Ranked profile recommendations |
| 5 | `05-client-config-artifacts.md` | Deterministic script work, frontier review | Chunk 4 | AI harness reference outputs |
| 6 | `06-end-to-end-validation.md` | Frontier or large-context | Chunks 1 through 5 | Final acceptance |

## Shared Contract

All chunks must follow `docs/06-next-phase-chunks/shared-contracts.md` for:

- repo-local agent file paths
- deterministic versus AI-assisted boundaries
- validation target expectations
- artifact layout and traceability fields
- runner, probe, evaluation, recommendation, and client artifact schemas

## Dispatch Rules

- Give every agent `docs/06-next-phase-handoff.md` and its chunk document.
- Give every agent `docs/06-next-phase-chunks/shared-contracts.md`.
- Agents should not widen scope into later chunks unless required to define stable interfaces.
- If a chunk needs to change an interface from a prior chunk, document the change in the chunk output and update the prior contract if needed.
- Every chunk must leave runnable validation commands or a clear explanation when live oMLX validation was not possible.
- Unsupported assistant-model paths are evidence, not failure, unless the chunk specifically tests failure handling.
- Generated AI harness reference artifacts are recommendations for operator review only. Chunks must not auto-apply or mutate actual user, workspace, client, or oMLX configuration unless the operator explicitly asks for that in a later task.

## Shared Acceptance Criteria

A chunk is acceptable only if it:

- preserves the repo's model-agnostic posture
- keeps deterministic steps scriptable and reproducible
- follows the shared artifact contracts or explicitly updates them with a compatible replacement
- records assumptions and unsupported paths explicitly
- writes outputs under the existing repository conventions or updates docs when adding new conventions
- avoids hardcoding `gemma-4-12B-it-bf16` except as the first validation target or fixture example

## Shared Definition Of Done

A chunk is done when:

- its promised files or changes exist in the repo
- validation has been run where possible
- the final agent response lists changed files and validation status
- any follow-up work is assigned to a later chunk or documented as a known limitation
