# mlx-pep

mlx-pep assesses local oMLX-hosted models, saves derived local profiles, generates harness-facing configuration guidance, and lets you review previous complete runs from the CLI or terminal UI.

Try this:

```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- assess mlx-community/NVIDIA-Nemotron-3.5-Lightning-30B-A3B-4bit --suite smoke
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- results show --model mlx-community/NVIDIA-Nemotron-3.5-Lightning-30B-A3B-4bit
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- apply ~/.mlx-pep/profiles/<timestamp>/<model>/profiles.jsonl --harness vscode --dry-run
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- tui
```

mlx-pep is built around a .NET CLI and a terminal browser. The assessment pipeline delegates to the adjacent Python `model-assessor` workflow, saves local profiles, emits markdown-table-heavy summaries, and generates structured client guidance for humans, scripts, and AI agents.

---

## What mlx-pep does

- Assess a local oMLX model against smoke or full benchmark suites.
- Save derived `high`, `balanced`, and `efficient` local profiles.
- Generate local client guidance artifacts for VS Code, VS Code Insiders, Claude Code, GitHub Copilot CLI, and OpenCode.
- Show previous verified-complete local runs as markdown tables or JSON.
- Export run summaries as markdown or JSON.
- Apply saved profiles to supported harness targets in dry-run or write mode.

## Build and run

```bash
dotnet build src/MlxPep.Cli/MlxPep.Cli.csproj
dotnet build src/MlxPep.Tui/MlxPep.Tui.csproj
```

Run the CLI:

```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- help
```

Run the standalone terminal browser:

```bash
dotnet run --project src/MlxPep.Tui/MlxPep.Tui.csproj
```

## Core workflow

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
