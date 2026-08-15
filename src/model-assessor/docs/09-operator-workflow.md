# Operator Workflow

This document is the long-lived operator workflow for assessing a model with this repository.

It describes how to use the implemented scripts and artifacts without depending on the temporary chunk plans.

## Workflow Modes

### Normal validation mode

Use one live oMLX instance and run one exact scenario at a time.

This is the recommended mode when:

- validating a new model
- collecting evidence with operator review
- iterating on long or deep scenarios
- avoiding the operational overhead of multiple concurrently hosted instances

### Concurrent hosting mode

Use topology-aware separate-port hosting only when you explicitly want multiple workload-specific configurations available at the same time.

## Inputs

The implemented workflow supports these operator inputs:

- `model-id`
- `assistant-model-id` when applicable
- `mtp=on|off|profile`
- `suite=smoke|full|single`
- `profile-id` for exact profile runs
- `base-url` when not using the default local instance
- `results-dir` when writing outside the default tree

For VLM-backed MTP profiles, `assistant-model-id` is not optional in practice. The runners now reject `*_mtp_on` VLM runs that omit an explicit assistant model because clean-start validation must not depend on inherited `vlm_mtp_draft_model` state inside the live oMLX instance.

## Phase 1: Benchmark And Probe Collection

### Smoke suite

Use this for a quick pass across the predefined smoke profiles.

```bash
python3 scripts/next_phase/run_assessment.py \
  --model-id <model-id> \
  --assistant-model-id <assistant-model-id> \
  --suite smoke \
  --mtp profile
```

If your smoke selection contains only MTP-off profiles, you may omit `--assistant-model-id`.

### Full matrix

Use this when you want the full benchmark profile set.

```bash
python3 scripts/next_phase/run_assessment.py \
  --model-id <model-id> \
  --assistant-model-id <assistant-model-id> \
  --suite full \
  --mtp profile
```

Use an explicit assistant model whenever the selected suite includes VLM-backed `*_mtp_on` profiles.

### Exact single-profile run

Use this for scenario-based validation.

```bash
python3 scripts/next_phase/run_assessment.py \
  --model-id <model-id> \
  --assistant-model-id <assistant-model-id> \
  --suite single \
  --profile-id <profile-id> \
  --mtp profile
```

For the current first validation target, the usual assistant value is `gemma-4-12B-it-assistant-bf16` when you intentionally run an MTP-on profile.

### Probe-only run

Use this when you want to validate inventory and guarded assistant probing without running benchmarks.

```bash
python3 scripts/next_phase/run_assessment.py \
  --model-id <model-id> \
  --assistant-model-id <assistant-model-id> \
  --suite single \
  --profile-id <profile-id> \
  --mtp profile \
  --probe-only
```

### Topology-aware run

Use this only when you want the runner to consume a previously declared topology contract.

```bash
python3 scripts/next_phase/run_assessment.py \
  --model-id <model-id> \
  --assistant-model-id <assistant-model-id> \
  --suite single \
  --profile-id <profile-id> \
  --mtp profile \
  --topology-manifest <recommendation-or-topology-json>
```

## Phase 2: Prompt-Quality Evaluation

### Exact case execution

```bash
python3 scripts/next_phase/run_prompt_evals.py \
  --model-id <model-id> \
  --assistant-model-id <assistant-model-id> \
  --profile-id <profile-id> \
  --case-id <case-id>
```

Include `--assistant-model-id` whenever the selected profile enables VLM-backed MTP.

### Bounded long or deep execution

```bash
python3 scripts/next_phase/run_prompt_evals.py \
  --model-id <model-id> \
  --assistant-model-id <assistant-model-id> \
  --profile-id <profile-id> \
  --case-id <case-id> \
  --max-tokens-override 512
```

### Offline validation

```bash
python3 scripts/next_phase/run_prompt_evals.py \
  --model-id dry-run-model \
  --profile-id <profile-id> \
  --validate-only
```

## Phase 3: Normalize And Recommend

Run this after you have enough benchmark and evaluation evidence for the batch you care about.

```bash
python3 scripts/next_phase/generate_recommendation_report.py \
  --model-id <model-id>
```

Expected outputs:

- `results/normalized/<normalization-id>/normalized_manifest.json`
- `results/recommendations/<recommendation-id>/recommendation_manifest.json`
- `results/summaries/<recommendation-id>.md`

## Phase 4: Generate Client Guidance

```bash
python3 scripts/next_phase/generate_client_config_artifacts.py \
  --recommendation-manifest results/recommendations/<recommendation-id>/recommendation_manifest.json
```

Expected outputs:

- `results/client-configs/<recommendation-id>/README.md`
- `results/client-configs/<recommendation-id>/client_recommendations.json`
- `results/client-configs/<recommendation-id>/ai-harness-reference.md`
- `results/client-configs/<recommendation-id>/unsupported-settings.md`

`ai-harness-reference.md` is the primary manual-testing artifact. It gives one table with the official terms used by VS Code, VS Code Insiders, Claude Code, GitHub Copilot CLI, and OpenCode beside the recommended model, instance, and oMLX settings for each workload row.

## Recommended Current Operator Path

Until the single-instance validation batch is complete, use this sequence:

1. pick one exact scenario,
2. run one exact benchmark profile,
3. run the matching prompt-quality case,
4. review the artifacts,
5. move to the next scenario,
6. regenerate recommendations only after the batch or at a deliberate checkpoint.

The current working tracker for that process is:

- `results/summaries/2026-06-11-gemma-4-12b-it-bf16-single-instance-validation-plan.md`

## What Not To Do

- Do not infer assistant compatibility from model cards alone.
- Do not run VLM-backed `*_mtp_on` profiles without an explicit assistant model ID.
- Do not treat AI harness reference artifacts as auto-applied configuration.
- Do not regenerate recommendations after every trivial scenario unless you are intentionally comparing checkpoints.
- Do not assume that multi-instance hosting is required for validation; it is only needed for concurrent workload-specific hosting.

## Review Checkpoints

Review after each scenario:

- was the run successful, partial, or failed?
- did the emitted settings match expectations?
- is prompt-quality evidence sufficient?
- is there a blocking defect or only a later design question?

Review after each batch:

- do the recommendation rankings still make sense?
- did repeated scenario issues suggest a contract problem?
- should bounded token defaults or case definitions change?
- should topology guidance change for normal operator use?
