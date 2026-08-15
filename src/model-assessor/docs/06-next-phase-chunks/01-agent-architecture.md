# Chunk 1: Agent Architecture And Contracts

## Purpose

Define the repo-local VS Code custom agent architecture for `Local Model Assessor` and the stable contracts that later chunks will implement.

## Context To Read

- `docs/06-next-phase-handoff.md`
- `docs/06-next-phase-chunks/shared-contracts.md`
- `docs/01-omlx-api-validation.md`
- `docs/02-findings-and-decisions.md`
- `results/README.md`
- existing `scripts/` and `config/` entry points

## Scope

This chunk should create the architectural spine for the next phase. It should not implement the full benchmark runner, prompt evaluator, report generator, or client config generator beyond interfaces and placeholders needed for later chunks.

## Required Outputs

- A repo-local VS Code custom agent definition for `Local Model Assessor`.
- Supporting instructions or skill files needed by the agent, if the repo convention chosen by this chunk requires them.
- The explicit repo-local scaffold named in `shared-contracts.md`, or a documented replacement convention updated in that file before implementation proceeds.
- A concise architecture document describing:
  - agent responsibilities
  - deterministic script responsibilities
  - artifact layout
  - interfaces between chunks
  - model-tiering guidance
  - oMLX-versus-Hugging-Face authority rules for assistant availability
- Shared schema and handoff details for Chunks 2 through 5, including runner outputs, prompt-evaluation outputs, recommendation manifests, client artifacts, and traceability keys.
- Any directory scaffolding needed for later chunks.

## Acceptance Criteria

- The agent definition names `Local Model Assessor` and reflects the chunk-0 decisions from the handoff.
- The agent file path, supporting instruction path, prompt path, and architecture document path are explicit and match `shared-contracts.md` unless that file is updated.
- The design makes deterministic scripts authoritative for benchmark execution, evaluation execution, normalization, and manifest emission.
- The design reserves AI reasoning for model-card research, candidate enrichment, evidence interpretation, and final recommendation text.
- The architecture supports target model ID, optional assistant model ID, explicit MTP state, and workload-profile selection.
- The architecture is model-agnostic and does not hardcode Gemma except as the first validation target.
- The artifact layout covers raw runs, prompt evaluations, summaries, recommendation manifests, and AI harness reference artifacts.
- Later chunks can implement against documented inputs and outputs without rereading the whole repository.
- The Hugging Face/model-card enrichment flow has an owner and output artifact or documented handoff to Chunk 2.
- The Chunk 2 handoff includes runner CLI expectations, assistant probe output format, artifact paths, and error handling rules for unsupported models or fields.

## Definition Of Done

- Agent/scaffold files exist in the repo at the documented paths.
- The architecture document is linked from `docs/06-next-phase-handoff.md` or the dispatch index.
- Any new repo conventions are documented.
- `shared-contracts.md` is still accurate after Chunk 1, or has been updated with any intentionally changed path/schema.
- Markdown diagnostics are clean where tooling is available.
- The final response lists exact files created or changed and identifies Chunk 2 as the next dependency.

## Launch Prompt

You are implementing Chunk 1 of the model-assessor next phase. Read `docs/06-next-phase-handoff.md`, `docs/06-next-phase-chunks/shared-contracts.md`, and `docs/06-next-phase-chunks/01-agent-architecture.md`. Implement only the architecture, repo-local VS Code custom agent scaffold, and stable contracts needed by later chunks. Do not build the full runner, evaluator, report generator, or client config generator. Validate what you can and report changed files plus remaining handoff points.
