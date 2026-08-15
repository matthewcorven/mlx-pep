# AGENTS.md

## Repo intent

This repository is an evidence pack and benchmark harness for assessing oMLX-exposed local models. Most work here changes docs, JSON profile definitions, or the small automation surface in `scripts/`.

Start with [README.md](/Users/core/git/matthewcorven/model-assessor/README.md), then follow the linked docs instead of duplicating their content in agent output:

- [docs/01-omlx-api-validation.md](/Users/core/git/matthewcorven/model-assessor/docs/01-omlx-api-validation.md) for verified API behavior and auth rules
- [docs/02-findings-and-decisions.md](/Users/core/git/matthewcorven/model-assessor/docs/02-findings-and-decisions.md) for current operating assumptions
- [docs/03-test-matrix.md](/Users/core/git/matthewcorven/model-assessor/docs/03-test-matrix.md) for the full benchmark matrix
- [docs/04-smoke-suite.md](/Users/core/git/matthewcorven/model-assessor/docs/04-smoke-suite.md) for the fast verification suite
- [docs/05-prompt-templates.md](/Users/core/git/matthewcorven/model-assessor/docs/05-prompt-templates.md) for phase-2 prompts
- [docs/06-next-phase-handoff.md](/Users/core/git/matthewcorven/model-assessor/docs/06-next-phase-handoff.md) for the next-agent requirements
- [results/README.md](/Users/core/git/matthewcorven/model-assessor/results/README.md) for output layout and summary shape

## Working rules

- Do not invent an `omlx benchmark` CLI flow. Benchmark automation in this repo is built around the oMLX admin HTTP API and the harness in `scripts/omlx_bench_harness.py`.
- Treat benchmark settings as explicit and reproducible. When changing profile behavior, keep JSON config, wrappers, and docs aligned.
- Prefer updating the existing docs above instead of restating the same guidance elsewhere.
- Keep the repo model-agnostic. Changes must work for any oMLX-exposed model unless a doc explicitly marks a model-specific constraint.

## Entry points

- Environment template: [/.env.example](/Users/core/git/matthewcorven/model-assessor/.env.example)
- Harness: [/scripts/omlx_bench_harness.py](/Users/core/git/matthewcorven/model-assessor/scripts/omlx_bench_harness.py)
- Smoke wrapper: [/scripts/run_smoke_suite.sh](/Users/core/git/matthewcorven/model-assessor/scripts/run_smoke_suite.sh)
- Full matrix wrapper: [/scripts/run_full_matrix.sh](/Users/core/git/matthewcorven/model-assessor/scripts/run_full_matrix.sh)
- Profiles: [/config/benchmark_profiles.json](/Users/core/git/matthewcorven/model-assessor/config/benchmark_profiles.json)
- Smoke selection: [/config/smoke_suite.json](/Users/core/git/matthewcorven/model-assessor/config/smoke_suite.json)
- Prompt templates: [/config/prompt_templates.json](/Users/core/git/matthewcorven/model-assessor/config/prompt_templates.json)

## Local commands

Export the required environment first:

```bash
export OMLX_BASE_URL="http://127.0.0.1:8000"
export OMLX_API_KEY="..."
```

Common commands:

```bash
python3 scripts/omlx_bench_harness.py --help
./scripts/run_smoke_suite.sh --model-id gemma-4-12B-it-bf16
./scripts/run_full_matrix.sh --model-id gemma-4-12B-it-bf16
```

Focused validation after script edits:

```bash
python3 -m py_compile scripts/omlx_bench_harness.py
bash -n scripts/run_smoke_suite.sh
bash -n scripts/run_full_matrix.sh
```

## Repo-specific pitfalls

- `OMLX_API_KEY` is required for every harness or wrapper run.
- The admin API requires login plus session cookies; bearer auth alone is not enough for the admin routes.
- The harness intentionally reads current settings, merges overrides, and sends a full settings `PUT`; do not simplify that to sparse updates unless the server behavior is revalidated.
- Benchmark SSE events are useful for progress, but durable persistence should still come from the final `/results` fetch.
- Output should land under `results/runs/`, `results/evaluations/`, and `results/summaries/` using the conventions in [results/README.md](/Users/core/git/matthewcorven/model-assessor/results/README.md).

## Change guidance

- If you change workload classes or profile IDs, update both the machine-readable JSON under `config/` and the matching documentation in `docs/`.
- If you change harness request or result handling, confirm it still matches the verified API contract in [docs/01-omlx-api-validation.md](/Users/core/git/matthewcorven/model-assessor/docs/01-omlx-api-validation.md).
- If you add new result artifacts or reports, extend [results/README.md](/Users/core/git/matthewcorven/model-assessor/results/README.md) so future agents know the expected layout.
