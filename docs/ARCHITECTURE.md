# Architecture Overview

**For:** Developers, contributors, anyone understanding how mlx-pep works
**Updated:** 2026-08-15

---

## High-Level Data Flow

```
┌─────────────────────────────────────────────────────────────────┐
│  User (CLI / TUI)                                               │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ├─→ [Assess]   (run benchmarks on model)
                     │
                     ├─→ [Results]  (show previous runs)
                     │
                     ├─→ [Apply]    (push profile to VS Code/etc)
                     │
                     └─→ [TUI]      (interactive browser)
                     │
                     v
┌─────────────────────────────────────────────────────────────────┐
│  MlxPep.Core (Profile Logic + Harness Appliers)                │
│  ├─ HardwareProfileMatcher        (detect Mac capabilities)    │
│  ├─ ProfileValidator              (safety checks)              │
│  ├─ AssessmentRunStore            (local file cache)           │
│  └─ HarnessApplier                (VS Code, Copilot CLI, etc)  │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     v
┌─────────────────────────────────────────────────────────────────┐
│  Model-Assessor (Python subprocess)                             │
│  ├─ omlx_bench_harness.py         (oMLX admin + benchmarks)    │
│  ├─ run_smoke_suite.sh            (quick assessment)           │
│  └─ run_full_matrix.sh            (comprehensive assessment)   │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     v
┌─────────────────────────────────────────────────────────────────┐
│  External Services                                              │
│  ├─ oMLX Server         (model hosting + inference)            │
│  ├─ Hugging Face Hub    (model downloads)                      │
│  └─ HF Local Cache      (~/.cache/huggingface/hub)             │
└─────────────────────────────────────────────────────────────────┘
```

---

## Component Overview

### 1. **CLI & TUI** (`src/MlxPep.Cli/`, `src/MlxPep.Tui/`)

**CLI (`MlxPep.Cli`):**
- Entry point: `Program.cs` with command routing
- Commands: `assess`, `results`, `apply`, `models`, `tui`
- Uses `CliBuilder` to orchestrate commands
- Outputs: markdown tables, JSON, JSONL profiles

**TUI (`MlxPep.Tui`):**
- Terminal UI using Spectre.Console / Terminal.Gui
- Wraps the same `.Core` logic as CLI
- Provides interactive model/run browsing
- Keyboard-driven: navigate, view, export, re-assess

---

### 2. **Core** (`src/MlxPep.Core/`)

**Profile & Harness Logic:**

| Class | Responsibility |
|-------|-----------------|
| `Profile` | POCO for mlx-pep profile structure (timing, config) |
| `HardwareProfileMatcher` | Detect Mac chip (M1/M2/M3/M4) + memory → platform profile |
| `ProfileValidator` | Schema validation + safety checks on profiles |
| `ProfileReader` | Parse JSONL profile files |
| `HarnessApplyResult` | Output structure for apply operations |

**Harness Appliers:**

| Class | Target |
|-------|--------|
| `VscodeHarnessApplier` | VS Code settings.json + keybindings.json |
| `CopilotCliHarnessApplier` | Copilot CLI config (TBD: exact path) |
| `ClaudeCodeHarnessApplier` | Claude Code (TBD: exact format) |
| `OpenCodeHarnessApplier` | OpenCode extension config |

Each applier: reads profile → generates harness-specific config → outputs structured guidance (dry-run) or applies changes (write mode).

**Run Storage:**

| Class | Responsibility |
|-------|-----------------|
| `AssessmentRunStore` | Read/write completed assessment runs from disk |
| `PublishService` | Generate markdown/JSON summaries of runs |

Runs are stored at: `~/.mlx-pep/profiles/{timestamp}/{model_id}/` with output files:
- `client_recommendations.json` — structured harness guidance
- `run_summary.md` — human-readable markdown
- `profiles.jsonl` — saved profiles (one per line)

**Python Integration:**

| Class | Responsibility |
|-------|-----------------|
| `PythonEnvironmentManager` | Locate model-assessor scripts + .env |
| `RuntimeEngine` | Spawn Python subprocess, capture output |

---

### 3. **Model-Assessor** (`src/model-assessor/`)

**Python subprocess that runs benchmarks:**

- **omlx_bench_harness.py** — main entry point
  - Connects to oMLX server
  - Loads model into memory
  - Runs prompt/completions through inference
  - Measures tokens/second, latency, memory

- **run_smoke_suite.sh** — quick assessment (~30 sec)
  - Small prompt set
  - Single concurrency level
  - Samples 2-3 quantization levels

- **run_full_matrix.sh** — comprehensive assessment (~5 min)
  - Full prompt matrix
  - Multiple concurrency levels (1, 2, 4, 8)
  - All quantization/optimization combinations

Output: JSON file `client_recommendations.json` with:
```json
{
  "client_recommendation_rows": [
    {
      "profile_name": "high",
      "recommended_tokens_per_second": 45.2,
      "config": { "quantize": "q4", "num_threads": 8 }
    },
    ...
  ]
}
```

---

## Technology Choices

### Why .NET?

✅ **Cross-platform CLI** — same code on Mac/Windows/Linux
✅ **Strong typing** — catches profile config errors early
✅ **Terminal UI** — Spectre.Console provides tables, colors, structured output
✅ **JSON serialization** — System.Text.Json handles JSONL profiles
✅ **Task parallelism** — multi-threaded assessments if needed later

### Why Delegate to Python?

✅ **oMLX ecosystem** — existing Python scripts + community harnesses
✅ **Inference integration** — easier to connect to model runtimes in Python
✅ **Data pipeline** — numpy, pandas already available for benchmarking
✅ **Shell reuse** — existing shell scripts (smoke/full suite) stay as-is

---

## Data Structures

### Profile JSONL Format

Each line is a valid JSON object representing one profile:

```json
{
  "name": "high",
  "estimated_tokens_per_second": 45.2,
  "hardware_target": "apple_silicon_m3",
  "quantization": "q4",
  "threading": { "num_threads": 8 },
  "memory_estimate_mb": 14500,
  "estimated_latency_ms": 125,
  "notes": "Optimized for real-time inference"
}
```

**Harness appliers parse this** and generate target-specific config:
- VS Code → `settings.json` entry for model ID + inference args
- Copilot CLI → config file path + model settings
- Claude Code → environment variables + system prompt config

---

## Integration Points

### oMLX Admin API

mlx-pep connects via HTTP to oMLX's `/api/` endpoints:

| Endpoint | Used by | Purpose |
|----------|---------|---------|
| `/api/version` | Health check | Verify server is running |
| `/api/models` | `models list` | List cached models |
| `/api/models/{id}/load` | `models get` | Load model into GPU |
| `/api/models/{id}/unload` | `models get` | Free GPU memory |
| `/api/inference/stream` | model-assessor | Run benchmark prompts |

**Environment:**
- `OMLX_BASE_URL` — server address (default: http://127.0.0.1:8000)
- `OMLX_API_KEY` — authentication token

### Hugging Face Cache

Model artifacts are downloaded into:
- `HF_HUB_CACHE` — typically `~/.cache/huggingface/hub`
- mlx-pep reads this directory to list available models

---

## Assessment Flow (Internals)

### 1. Pre-assessment Checks
```
User: mlx-pep assess <model-id> --suite smoke
  ↓
[Validate model exists in HF cache OR fetch it]
  ↓
[Verify oMLX server is reachable]
  ↓
[Check hardware: M1/M2/M3/M4 + memory]
```

### 2. Spawn Model-Assessor
```
.NET Core → Python subprocess
  ↓
$ python3 omlx_bench_harness.py \
    --model <model-id> \
    --suite smoke \
    --output client_recommendations.json
  ↓
[oMLX loads model + runs benchmarks]
  ↓
[Produces client_recommendations.json]
```

### 3. Process Results
```
.NET reads client_recommendations.json
  ↓
[ProfileValidator checks schema]
  ↓
[HardwareProfileMatcher applies Mac-specific tuning]
  ↓
[Save to ~/.mlx-pep/profiles/{timestamp}/{model_id}/]
  ↓
[Generate client recommendations for each harness]
  ↓
[Output markdown summary or JSON]
```

---

## Run Persistence

Completed runs are stored in `~/.mlx-pep/profiles/`:

```
~/.mlx-pep/profiles/
├── 2026-08-15T10-30-00Z/
│   └── mlx-community/Llama-2-7b-hf/
│       ├── profiles.jsonl              (high, balanced, efficient)
│       ├── client_recommendations.json (raw benchmark output)
│       ├── run_summary.md              (markdown table)
│       └── run_summary.json            (JSON export)
│
└── 2026-08-14T18-45-00Z/
    └── mlx-community/phi-3.5-mini/
        ├── profiles.jsonl
        └── ...
```

**`AssessmentRunStore`** provides:
- `ListRuns()` — enumerate all completed runs
- `GetLatestRun(modelId)` — fetch most recent for a model
- `SaveRun(metadata, profiles)` — persist new run
- `ExportRun(runId, format)` — convert to markdown/JSON

---

## Harness Applier Protocol

Each applier implements the same interface:

```csharp
public interface IHarnessApplier
{
    Task<HarnessApplyResult> ApplyAsync(
        IEnumerable<Profile> profiles,
        bool dryRun = true);
}
```

**Workflow:**
1. Parse profiles from JSONL
2. Select the appropriate profile (high/balanced/efficient) for target harness
3. Generate harness-specific config (settings.json, env vars, etc.)
4. If `dryRun=true`: output what would be applied
5. If `dryRun=false`: apply changes + backup original config

Each harness returns structured guidance:
```json
{
  "status": "success|warning|error",
  "messages": ["Applied profile to VS Code", "..."],
  "applied_settings": { "model": "...", "tokens_per_second": 45 },
  "backup_location": "~/.mlx-pep/backups/vscode_2026-08-15.json"
}
```

---

## Extension Points

### Adding a New Harness

1. Create `src/MlxPep.Core/XyzHarnessApplier.cs` implementing `IHarnessApplier`
2. Add a `case "xyz":` handler in `CliBuilder.Apply()`
3. Document target config file/env vars in `docs/ARCHITECTURE.md`
4. Add tests in `tests/MlxPep.Core.Tests/`

### Adding a New Benchmark

1. Add script to `src/model-assessor/scripts/`
2. Update `run_smoke_suite.sh` or `run_full_matrix.sh` to call it
3. Modify `client_recommendations.json` output format (if needed)
4. Update `ProfileValidator` to accept new fields

---

## Next Reading

- [Quick Start](QUICK-START.md) — get up and running
- [Contributing Guide](../README.md#contributing) — join development
- [Profile Schema](profile-schema.md) — detailed JSONL spec
