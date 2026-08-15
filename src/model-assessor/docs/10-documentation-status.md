# Documentation Status

This document distinguishes long-lived repository documentation from temporary planning and implementation material.

The goal is to make future cleanup straightforward without losing stable operational knowledge.

## Long-Lived Documentation

These files should remain as the durable reference set for the repository.

### Core behavior and evidence model

- `README.md`
- `docs/01-omlx-api-validation.md`
- `docs/02-findings-and-decisions.md`
- `docs/03-test-matrix.md`
- `docs/04-smoke-suite.md`
- `docs/05-prompt-templates.md`
- `docs/08-repository-architecture.md`
- `docs/09-operator-workflow.md`
- `results/README.md`

### Living operational summaries

- final readiness reports under `results/summaries/` when they describe current accepted behavior
- recommendation summaries under `results/summaries/` when they correspond to still-relevant recommendation manifests

## Temporary Documentation Still Retained

These files are still needed during the current validation period, but they are not intended to remain the primary long-term explanation surface.

### Planning and handoff material

- `docs/06-next-phase-handoff.md`
- everything under `docs/06-next-phase-chunks/`
- `docs/07-local-model-assessor-architecture.md`

### Working validation artifact

- `results/summaries/2026-06-11-gemma-4-12b-it-bf16-single-instance-validation-plan.md`
- temporary copy-and-run prompt documents under `results/summaries/` for reusable and scenario-specific validation restarts

This working validation plan is intentionally temporary even though it lives under `results/summaries/`. It is an execution tracker for the current operator-assisted validation batch.

The temporary prompt documents are also intentionally short-lived. They exist only to give the operator clean copyable prompts while the single-instance validation batch is in progress.

## Why The Temporary Docs Still Matter

The temporary docs are still useful because they preserve:

- the original chunked implementation intent
- explicit contract reasoning for runner, evaluation, normalization, and client outputs
- acceptance criteria that are still being checked during the single-instance scenario batch

They should not be deleted until the validation plan scenarios are complete and any resulting corrections have been folded into the long-lived docs.

## Retirement Criteria For Temporary Docs

The temporary planning docs can be reduced, archived, or removed only after all of the following are true:

1. the single-instance validation plan scenarios are complete,
2. the resulting script behavior is accepted,
3. stable behavior is fully reflected in the long-lived docs,
4. no remaining active work depends on the old chunk-by-chunk guidance.

## Update Rules

### Update the long-lived docs when

- implemented workflow behavior changes
- a CLI contract changes
- artifact layout changes
- operator guidance changes
- runtime constraints change materially

### Update the temporary docs when

- active validation still depends on them
- current in-flight work would become misleading without the update

## Practical Rule

If a document explains how the repository should be used after the current validation project ends, it belongs in the long-lived set.

If a document mainly explains how the repository was built, chunked, or temporarily validated during the current project phase, it belongs in the temporary set until cleanup time.
