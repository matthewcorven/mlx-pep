# Chunk 3: Synthetic Prompt-Quality Evaluations

## Purpose

Create repeatable synthetic fixtures and a deterministic prompt-quality evaluation runner so profile comparisons include output usefulness, not only synthetic benchmark metrics.

## Context To Read

- `docs/06-next-phase-handoff.md`
- `docs/06-next-phase-chunks/01-agent-architecture.md`
- `docs/06-next-phase-chunks/shared-contracts.md`
- `docs/05-prompt-templates.md`
- `config/prompt_templates.json`
- `results/README.md`

## Scope

This chunk owns synthetic fixtures, prompt case definitions, expected answers, evaluation execution, and raw evaluation artifact persistence. It should not implement final recommendation ranking or AI harness reference artifacts.

## Required Outputs

- A mini synthetic fixture repository or fixture tree suitable for tool-using research and coding prompts.
- Prompt case definitions that bind the existing workload templates to concrete placeholder values.
- Expected-answer metadata for each case, including objective checks where possible and human-review notes where judgment is required.
- A deterministic evaluation runner or script interface matching the prompt-quality evaluation contract in `shared-contracts.md`.
- Fixture manifest metadata with a fixture version and hash that changes whenever fixture files, prompt cases, or expected-answer definitions change.
- Documentation for running prompt-quality evaluations.

## Implemented Surface

- Synthetic fixture tree: `fixtures/synthetic_repo/`
- Fixture manifest: `fixtures/synthetic_repo/fixture_manifest.json`
- Prompt cases: `config/evaluation_cases.json`
- Deterministic runner: `scripts/next_phase/run_prompt_evals.py`
- Practical live fixture prep: `scripts/next_phase/prepare_practical_live_fixtures.py`
- Practical live fixture root: `fixtures/practical_live/`
- Practical targeted cases: `config/practical_evaluation_cases.json`

Offline validation commands that must work without live oMLX:

```bash
python3 scripts/next_phase/run_prompt_evals.py \
  --model-id dry-run-model \
  --profile-id short_code_research_tools_mtp_off \
  --list-cases

python3 scripts/next_phase/run_prompt_evals.py \
  --model-id dry-run-model \
  --profile-id short_code_research_tools_mtp_off \
  --validate-only

python3 scripts/next_phase/run_prompt_evals.py \
  --model-id dry-run-model \
  --profile-id short_code_research_tools_mtp_off \
  --dry-run
```

Live evaluation, when oMLX is available:

```bash
python3 scripts/next_phase/run_prompt_evals.py \
  --model-id gemma-4-12B-it-bf16 \
  --profile-id short_code_research_tools_mtp_off
```

The live path first applies the selected profile settings through the admin API using login plus cookie auth, then calls the public `/v1/completions` endpoint with `OMLX_API_KEY` bearer auth. Live evaluation runs therefore include `admin/` artifacts for login, profile fields, model inventory, selected model, and settings request or response alongside the raw output and derived scoring files. Raw model outputs remain under `raw/`, while automatic checks and review-oriented metadata are written separately under `derived/`.

For practical live validation of long or deep profiles, the runner also supports `--max-tokens-override <n>`. When used, the emitted evaluation artifacts record the overridden `max_tokens` value as part of the effective generation settings so the run remains traceable and reproducible.

For immediate practical use, prepare the practical fixture root first:

```bash
python3 scripts/next_phase/prepare_practical_live_fixtures.py
```

That flow clones or refreshes the latest default branch of `https://github.com/microsoft/aspire`, writes a deterministic context bundle for the Zig AppHost planning scenario, and refreshes the local practical briefs for the long-coding and deep-research scenarios.

Then run the targeted practical cases against that fixture root with the dedicated case catalog:

The practical catalog already includes both `*_mtp_off` and `*_mtp_on` variants for all three scenarios:

- `aspire-zig-apphost-planning-mtp-off` and `aspire-zig-apphost-planning-mtp-on`
- `nextjs-commerce-aspire-ts-mtp-off` and `nextjs-commerce-aspire-ts-mtp-on`
- `echo-show-home-assistant-viability-mtp-off` and `echo-show-home-assistant-viability-mtp-on`

The examples below show the `*_mtp_off` commands. For direct MTP quality comparisons, rerun the same command with the matching `--profile-id ..._mtp_on` and `--case-id ...-mtp-on` values.

```bash
python3 scripts/next_phase/run_prompt_evals.py \
  --model-id gemma-4-12B-it-bf16 \
  --profile-id long_code_research_tools_mtp_off \
  --cases config/practical_evaluation_cases.json \
  --fixture-root fixtures/practical_live \
  --skip-workload-coverage-check \
  --case-id aspire-zig-apphost-planning-mtp-off \
  --max-tokens-override 512
```

```bash
python3 scripts/next_phase/run_prompt_evals.py \
  --model-id gemma-4-12B-it-bf16 \
  --profile-id long_coding_mtp_off \
  --cases config/practical_evaluation_cases.json \
  --fixture-root fixtures/practical_live \
  --skip-workload-coverage-check \
  --case-id nextjs-commerce-aspire-ts-mtp-off \
  --max-tokens-override 512
```

```bash
python3 scripts/next_phase/run_prompt_evals.py \
  --model-id gemma-4-12B-it-bf16 \
  --profile-id deep_research_mtp_off \
  --cases config/practical_evaluation_cases.json \
  --fixture-root fixtures/practical_live \
  --skip-workload-coverage-check \
  --case-id echo-show-home-assistant-viability-mtp-off \
  --max-tokens-override 512
```

## Acceptance Criteria

- Fixtures are controlled, small, text-only, under 5 MB, and repeatable.
- Every workload class has at least one prompt-quality case.
- Prompt cases record model ID, optional assistant model ID, profile ID, MTP state, settings, prompt text, output text, and evaluation metadata.
- The runner separates raw model outputs from scoring or interpretation.
- Expected answers avoid overfitting to a single wording and instead identify required facts, forbidden claims, and useful quality signals.
- Expected-answer metadata follows the schema in `shared-contracts.md`.
- Fixture changes are auditable through version/hash updates and documented change notes.
- The output layout is compatible with `results/evaluations/` or updates `results/README.md`.
- The design remains usable for any oMLX-exposed model.
- Evaluation execution and raw output capture are deterministic script work. AI may be used later for interpretation, but not as the authority for case execution or JSON validity.

## Definition Of Done

- Fixture files, prompt case config, runner code, and docs exist in the repo.
- Syntax validation passes for changed scripts and JSON files.
- At least one non-live dry-run, listing, or fixture validation command works without requiring oMLX.
- If live oMLX evaluation is not run, the final response gives exact commands for running it.
- `shared-contracts.md` remains accurate for evaluation outputs, or is updated with any intentional compatible change.
- The final response lists changed files and identifies Chunk 4 as the consumer of these artifacts.

## Launch Prompt

You are implementing Chunk 3 of the model-assessor next phase. Read `docs/06-next-phase-handoff.md`, `docs/06-next-phase-chunks/01-agent-architecture.md`, `docs/06-next-phase-chunks/shared-contracts.md`, and `docs/06-next-phase-chunks/03-synthetic-evaluations.md`. Build the synthetic fixture set and prompt-quality evaluation runner only. Keep scoring metadata structured but do not build the final recommendation report. Do not use LLM reasoning as the authority for fixture execution or raw evaluation capture. Validate fixtures and scripts, then report changed files and run commands.
