# mlx-pep

**Assess local models. Save profiles. Configure harnesses. Ship smarter.**

mlx-pep is a model assessment tool for Apple Silicon Macs. Run your oMLX-hosted models against a benchmark suite, get performance profiles (high/balanced/efficient), and apply them to VS Code, GitHub Copilot CLI, and other code editors.

---

## Why mlx-pep?

### The Problem

You've installed MLX and are hosting models locally on your Mac. But how do you know which model runs fast enough for real-time code completion? What settings should you use? Which quantization level balances quality and speed for YOUR hardware?

Manual tuning is frustrating. Benchmark scripts exist, but translating results to editor config is manual and error-prone.

### The Solution

mlx-pep automates this:

1. **Assess** your model on your Mac (smoke suite: 30 sec | full suite: 5 min)
2. **Get three profiles** tuned for your hardware (high/balanced/efficient)
3. **Apply to your editor** — mlx-pep handles the harness-specific config
4. **Repeat** when you upgrade hardware or try new models

**Result:** You spend 5 minutes per model and get production-ready config, not guesswork.

---

## Quick Start (5 minutes)

### 1. Check prerequisites

```bash
# Apple Silicon Mac (M1/M2/M3/M4) — required
# .NET 10.0+ — required
dotnet --version

# oMLX server running — required
curl http://127.0.0.1:8000/api/version
```

### 2. Build mlx-pep

```bash
git clone https://github.com/matthewcorven/mlx-pep.git
cd mlx-pep
dotnet build src/MlxPep.Cli/MlxPep.Cli.csproj
```

### 3. Run your first assessment

```bash
export OMLX_BASE_URL=http://127.0.0.1:8000
export OMLX_API_KEY=<your-key>

dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- \
  assess mlx-community/Llama-2-7b-hf --suite smoke
```

### 4. View results

```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- \
  results show --model mlx-community/Llama-2-7b-hf
```

**Next:** [Full Quick Start Guide](docs/QUICK-START.md) with detailed troubleshooting.

---

## Concepts

### Profile

A **profile** is a tuned configuration for a model on your specific Mac:

```json
{
  "name": "balanced",
  "estimated_tokens_per_second": 38.1,
  "quantization": "q4",
  "num_threads": 6,
  "memory_estimate_mb": 14500
}
```

mlx-pep generates three profiles per model (high/balanced/efficient) reflecting different hardware tuning choices.

### Assessment

An **assessment** is one complete benchmark run for a model:

- **Smoke suite**: Quick (30 sec), tests common scenarios
- **Full suite**: Comprehensive (5 min), tests all combinations

Assessments measure: tokens/sec, latency, memory usage, quality (perplexity).

### Harness

A **harness** is a target application that uses the model:

| Harness | Config Type |
|---------|-------------|
| VS Code | `.vscode/settings.json` |
| Copilot CLI | `~/.copilot/config.json` |
| Claude Code | Environment variables |
| OpenCode | Extension settings |

When you "apply" a profile, mlx-pep translates it to the harness's native config format.

---

## Installation

### Prerequisites

- **macOS 13.4+** (Ventura, Sonoma, or Sequoia)
- **Apple Silicon** (M1, M2, M3, M4)
- **.NET 10.0 SDK** — install via [dotnet.microsoft.com](https://dotnet.microsoft.com/en-us/download) or Homebrew:
  ```bash
  brew install dotnet@10
  ```
- **oMLX server running** (see [mlx-lm docs](https://ml-explore.github.io/mlx/build/latest/index.html))

### Get the code

```bash
git clone https://github.com/matthewcorven/mlx-pep.git
cd mlx-pep
dotnet build src/MlxPep.Cli/MlxPep.Cli.csproj
```

### Set environment

```bash
export OMLX_BASE_URL=http://127.0.0.1:8000
export OMLX_API_KEY=<your-omlx-api-key>
export HF_HUB_CACHE=~/.cache/huggingface/hub
```

### Verify setup

```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- help
```

See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for development environment setup.

---

## Feature Status

### ✅ MVP (Current)

- Smoke + full benchmark suites for MLX models
- Three profile tiers (high/balanced/efficient)
- Apply to VS Code, Copilot CLI, Claude Code, OpenCode
- Result browser (CLI + TUI)
- Export as markdown/JSON

### 🟡 Fast-Follow (Q3-Q4 2026)

- **Community profile browser** — share and reuse profiles
- **Batch assessment mode** — profile multiple models at once
- **Linux/ARM64 support** — extend beyond Apple Silicon
- **AWS Lambda harness** — serverless model hosting
- **Background assessment mode** — non-blocking CLI
- **Custom benchmark suites** — user-defined test sets

### 🔲 Future (Roadmap)

- Hardware auto-detection and recommendation
- Per-harness tuning (not just generic profiles)
- Model comparison dashboard
- Integration with MLX registry for upstream recommendations

---

## What mlx-pep Does

- Assess a local oMLX model against smoke or full benchmark suites.
- Save derived `high`, `balanced`, and `efficient` local profiles.
- Generate local client guidance artifacts for VS Code, VS Code Insiders, Claude Code, GitHub Copilot CLI, and OpenCode.
- Show previous verified-complete local runs as markdown tables or JSON.
- Export run summaries as markdown or JSON.
- Apply saved profiles to supported harness targets in dry-run or write mode.

---

## What mlx-pep does

- Assess a local oMLX model against smoke or full benchmark suites.
- Save derived `high`, `balanced`, and `efficient` local profiles.
- Generate local client guidance artifacts for VS Code, VS Code Insiders, Claude Code, GitHub Copilot CLI, and OpenCode.
- Show previous verified-complete local runs as markdown tables or JSON.
- Export run summaries as markdown or JSON.
- Apply saved profiles to supported harness targets in dry-run or write mode.

---

## Usage

### Run the CLI

```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- help
```

### Run the terminal UI

```bash
dotnet run --project src/MlxPep.Tui/MlxPep.Tui.csproj
```

The TUI provides interactive browsing of models, assessments, and profiles.

VS Code debugging:

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

The TUI reads the same `OMLX_BASE_URL` and `OMLX_API_KEY` values as the CLI flow, and it is useful for browsing verified-complete assessment runs without leaving the terminal.

## Core workflow

### Inspect and download models

List the shared Hugging Face cache entries that mlx-pep can already see:

```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- models list
```

Start a model download through the live oMLX admin API and return immediately with the visible task id:

```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- models get <hf-model-id> --no-wait
```

Poll current download tasks and model load state:

```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- models status
```

Wait for a download to finish and optionally load it into memory once it lands in the shared oMLX store:

```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- models get <hf-model-id>
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- models get <hf-model-id> --load
```

### Assess a model

```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- assess <hf-model-id> --suite smoke
```

Optional assistant-model usage:

```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- assess <hf-model-id> --assistant-model-id <assistant-model-id> --suite smoke
```

Notes:

- If no assistant model is provided, mlx-pep forces the assessment pipeline into non-MTP mode.
- If the underlying oMLX model is not MTP-compatible, assistant-model runs fail honestly instead of falling back to synthetic data.

### Review local completed runs

List verified-complete runs:

```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- results list
```

Show the latest run for a model:

```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- results show --model <hf-model-id>
```

Export a run summary:

```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- results export --model <hf-model-id> --output ./run-summary.md --format markdown
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- results export <run-id> --output ./run-summary.json --format json
```

Machine-readable output:

```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- results list --json
```

### Apply a saved profile to a harness

Dry-run a harness apply:

```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- apply ~/.mlx-pep/profiles/<timestamp>/<model>/profiles.jsonl --harness vscode --dry-run
```

Supported harness values:

- `vscode`
- `copilot-cli`
- `opencode`
- `claude-code`

The apply path can consume the full saved profile set and derive multi-entry editor or CLI configuration bundles from it.

### Browse interactively

```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- tui
```

The interactive browser lets you:

- select a model with verified-complete local runs
- view the latest results summary
- save the summary as markdown or JSON
- run a new assessment
- list complete local runs for that model

## TUI user workflows and scenarios

The TUI is a presentation layer over the same command handlers used by the CLI. In other words, each TUI workflow is backed by the same command semantics and safety checks as the CLI, with the UI simply making it easier to browse and act.

### 1. Review a model and its latest completed run

Use case:
- a user wants to inspect the newest verified-complete assessment for a known model.

CLI entry points:
- `mlx-pep results list`
- `mlx-pep results show --model <hf-model-id>`
- `mlx-pep results export --model <hf-model-id> --output ./summary.md --format markdown`

Known scenarios:
- Happy path: the run exists and the summary renders with the last verified-complete state.
- Warning path: a run exists but is incomplete; the command can still display it only when `--all` is used.
- Edge case: the selected model has no completed run; the command exits with a clear "no results"/not-found message.

### 2. Download and inspect the shared oMLX model cache

Use case:
- a user wants to see what local models are already cached or start a new download.

CLI entry points:
- `mlx-pep models list`
- `mlx-pep models status`
- `mlx-pep models get <hf-model-id>`
- `mlx-pep models get <hf-model-id> --no-wait`

Known scenarios:
- Happy path: the cache contains at least one model and the table or JSON output is shown.
- Warning path: a download is accepted but the task is still in progress; the no-wait branch exits cleanly with a task id.
- Error path: the oMLX admin login or download request fails; the command exits non-zero and surfaces the underlying error.
- Edge case: the cache is empty; the list command prints an explicit empty-state message.

### 3. Run a fresh assessment for a model

Use case:
- a user wants to generate a new local assessment and save profiles.

CLI entry points:
- `mlx-pep assess <hf-model-id> --suite smoke`
- `mlx-pep assess <hf-model-id> --suite full`
- `mlx-pep assess <hf-model-id> --assistant-model-id <assistant-model-id> --suite smoke`

Known scenarios:
- Happy path: the model-assessor pipeline succeeds and local profile output is stored.
- Warning path: the assessment emits warnings or a partially valid profile set; the command still exits with a success result if the local validation for the requested workflow is acceptable.
- Error path: the underlying Python assessment scripts fail or the model-assessor is unavailable; the command exits non-zero with the relevant cause.
- Edge case: the assistant model is required for MTP-specific profiles but was omitted; the command fails politely rather than silently ignoring the profile requirement.

### 4. Apply a generated profile to a local harness

Use case:
- a user wants to mirror a saved profile into a client harness without writing real config in a live session.

CLI entry points:
- `mlx-pep apply <profile-file> --harness vscode --dry-run`
- `mlx-pep apply <profile-file> --harness copilot-cli --dry-run --no-confirm`

Known scenarios:
- Happy path: the profile is read and the dry-run output shows the exact configuration entries or actions that would be applied.
- Warning path: the profile contains unsupported or manual-only settings; the apply flow reports them clearly without pretending they are auto-written.
- Error path: the file is missing or not valid JSONL/profile content; the command exits non-zero with a concrete validation message.
- Edge case: a harness is passed that is not supported by the current profile; the apply command surfaces the mismatch.

### 5. Launch the interactive browser for the same local workflows

Use case:
- a user prefers a terminal UI instead of repeated CLI commands.

Entry points:
- `mlx-pep tui`

Known scenarios:
- Happy path: the TUI opens and allows browsing the same run, model, and summary flows as the terminal commands.
- Error path: the TUI is invoked with `--json` and refuses to start because it is a non-JSON interactive surface.
- Edge case: the environment is missing the local results or model-assessor data; the TUI still opens, but it shows empty or no-data states rather than failing unpredictably.

These cases are intentionally written to be directly testable at the command level so the CLI and the TUI remain aligned by user workflow rather than by hidden implementation details.

## Files written by assessment

| Output | Location |
| --- | --- |
| Local saved profiles | `~/.mlx-pep/profiles/<timestamp>/<model>/profiles.jsonl` |
| Assessment run artifacts | `src/model-assessor/results/mlx-pep-cli/<operation-id>/runs/<run-id>/...` |
| Normalized evidence | `src/model-assessor/results/mlx-pep-cli/<operation-id>/normalized/<normalization-id>/normalized_manifest.json` |
| Recommendation manifest | `src/model-assessor/results/mlx-pep-cli/<operation-id>/recommendations/<recommendation-id>/recommendation_manifest.json` |
| Client guidance bundle | `src/model-assessor/results/mlx-pep-cli/<operation-id>/client-configs/<recommendation-id>/...` |
| Markdown summary | `src/model-assessor/results/mlx-pep-cli/<operation-id>/summaries/<recommendation-id>.md` |

## Client guidance artifacts

Each completed assessment can generate a client-config bundle with:

- `client_recommendations.json` for scripts and AI agents
- `ai-harness-reference.md` for operator review
- `README.md` for the bundle overview
- `unsupported-settings.md` for settings that remain oMLX-side

These artifacts include real recorded values such as:

- `max_context_window`
- `max_tokens`
- `temperature`
- `top_p`
- `top_k`
- `min_p`
- harness-facing context and output token limits
- local base URLs such as `http://127.0.0.1:8000/v1`

## Legacy matrix script

The repository still includes the original read-only Apple Silicon matrix generator:

```bash
python3 generate_ornith_matrix.py
python3 generate_ornith_matrix.py --write current_matrix.md
python3 generate_ornith_matrix.py --json
```

That script is read-only. It does not unload oMLX models, modify load state, or uninstall anything.

## Community service deployment

The repo includes a minimal ASP.NET Core profile service at `src/MlxPep.Service`.

```bash
docker build -t mlxpep-service -f src/MlxPep.Service/Dockerfile .
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__AzureBlobStorage="<connection-string>" \
  mlxpep-service
```

## Related docs

- `docs/service-deployment.md`
- `docs/harness-apply-design.md`
- `docs/profile-schema.md`
- `src/model-assessor/docs/09-operator-workflow.md`
