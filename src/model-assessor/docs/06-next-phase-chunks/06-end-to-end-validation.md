# Chunk 6: End-To-End Validation And Final Handoff

## Purpose

Validate the complete next-phase workflow and produce the final operator handoff showing how to assess a target model and optional assistant model from discovery through AI harness reference outputs.

## Context To Read

- `docs/06-next-phase-handoff.md`
- all prior chunk documents
- `docs/06-next-phase-chunks/shared-contracts.md`
- all implementation outputs from Chunks 1 through 5
- `README.md`
- `results/README.md`

## Scope

This chunk owns integration validation, final docs, and readiness assessment. It should avoid major redesign unless earlier chunk outputs cannot satisfy the handoff success criteria.

Current repo status: the implementation is now complete enough for end-to-end validation of topology-aware live execution. Final validation should prove the live runner path, normalization outputs, and client artifacts stay aligned on the same separate-port multi-instance contract.

## Required Outputs

- An end-to-end validation run or documented dry-run covering:
  - model selection
  - optional assistant probing
  - benchmark execution path
  - prompt-quality evaluation path
  - normalization and report generation
  - AI harness reference artifact generation
- A smoke-suite validation against `gemma-4-12B-it-bf16` when live oMLX is available; full matrix validation is optional unless explicitly requested.
- Updated operator documentation for the complete workflow.
- A final readiness report with pass/fail status against the main handoff success criteria.
- A Markdown list of known limitations and recommended next improvements in the final readiness report.

## Acceptance Criteria

- A human operator can identify the command or agent workflow needed to assess `gemma-4-12B-it-bf16` and future oMLX-exposed models.
- The workflow can proceed target-only when assistant probing is unsupported.
- Raw evidence, normalized data, recommendations, and client artifacts are traceable to each other using the IDs and source paths from `shared-contracts.md`.
- The docs explain which steps require live oMLX and which can be run offline against fixtures or sample evidence.
- The final report does not claim stronger confidence than the available evidence supports.
- All changed scripts pass syntax validation.
- The repo remains consistent with `AGENTS.md` and avoids inventing an `omlx benchmark` CLI flow.
- Generated AI harness reference artifacts remain recommendations for operator review and are not auto-applied.
- If live oMLX or the first validation model is unavailable, the final report records the blocker, the exact commands to rerun, and the artifact paths expected from a successful run.

## Definition Of Done

- End-to-end commands have been run or explicitly documented as blocked by unavailable live services.
- README or handoff docs point to the complete workflow.
- Final readiness report exists under `results/summaries/` or another documented location.
- Known limitations are documented with owners or follow-up suggestions.
- `results/README.md` accurately describes any new result directories introduced by Chunks 1 through 6.
- The final response lists validation status, changed files, generated artifacts, and whether the next phase meets the success criteria.

## Launch Prompt

You are implementing Chunk 6 of the model-assessor next phase. Read `docs/06-next-phase-handoff.md`, all chunk documents, `docs/06-next-phase-chunks/shared-contracts.md`, and the outputs from Chunks 1 through 5. Validate the complete workflow from model selection through AI harness reference artifacts. Use the smoke suite against `gemma-4-12B-it-bf16` when live oMLX is available; document exact rerun commands if it is not. Update operator docs and produce a final readiness report. Do not perform broad redesign unless required to meet the documented success criteria. Report validation results and remaining limitations.
